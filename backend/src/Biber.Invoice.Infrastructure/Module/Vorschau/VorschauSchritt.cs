using System.Security.Cryptography;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Vorschau;

/// <summary>
/// Schritt 3c: Erzeugt das Vorschaubild der ersten Seite (600 px) und legt bzw. ersetzt die <see cref="BelegDatei"/> mit Rolle Vorschau.
/// Ein Fehler beim Rendern hält die Pipeline nicht auf.
/// </summary>
public sealed class VorschauSchritt(IVorschauDienst vorschau, BiberDbContext db, TimeProvider zeit, ILogger<VorschauSchritt> log)
    : IPipelineSchritt
{
    public const int Breite = 600;

    public int Reihenfolge => SchrittReihenfolge.Vorschau;

    public string Name => "Vorschau";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        byte[]? webp;
        try
        {
            vorschau.Verwerfen(beleg, 1, Breite);
            webp = await vorschau.SeiteAsync(beleg, 1, Breite, ct, kontext.DateiPfad);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "Vorschau für Beleg {BelegId} konnte nicht erzeugt werden", beleg.Id);
            return SchrittErgebnis.Weiter;
        }

        if (webp is null)
        {
            return SchrittErgebnis.Weiter;
        }

        var datei = db.BelegDateien.Local.FirstOrDefault(d => d.BelegId == beleg.Id && d.Rolle == DateiRolle.Vorschau)
            ?? await db.BelegDateien.FirstOrDefaultAsync(d => d.BelegId == beleg.Id && d.Rolle == DateiRolle.Vorschau, ct);
        if (datei is null)
        {
            datei = new BelegDatei { Id = Guid.NewGuid(), BelegId = beleg.Id, Rolle = DateiRolle.Vorschau };
            db.BelegDateien.Add(datei);
        }

        datei.Pfad = vorschau.RelativerPfad(beleg, 1, Breite);
        datei.MimeType = "image/webp";
        datei.Sha256 = Convert.ToHexStringLower(SHA256.HashData(webp));
        datei.Groesse = webp.Length;
        datei.AbgelegtAm = zeit.GetUtcNow();
        return SchrittErgebnis.Weiter;
    }
}
