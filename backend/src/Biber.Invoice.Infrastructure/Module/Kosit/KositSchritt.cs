using System.Security.Cryptography;
using System.Text;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Biber.Invoice.Infrastructure.Module.Kosit;

/// <summary>
/// Schritt 3a: Validiert die E-Rechnung beim KoSIT-Sidecar und legt ihre HTML-Visualisierung unter
/// <c>Pfade:Daten/visualisierung/{BelegId}.html</c> ab (eine <see cref="BelegDatei"/> mit Rolle Visualisierung je Beleg).
/// </summary>
public sealed class KositSchritt(IKositClient kosit, BiberDbContext db, IConfiguration konfiguration, TimeProvider zeit)
    : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.Kosit;

    public string Name => "KoSIT-Validierung";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        if (kontext.ERechnungXml is not { Length: > 0 } xml)
        {
            return SchrittErgebnis.Weiter;
        }

        KositErgebnis ergebnis;
        string html;
        try
        {
            ergebnis = await kosit.ValidierenAsync(xml, ct);
            html = await kosit.VisualisierenAsync(xml, ct);
        }
        catch (KositNichtErreichbarException)
        {
            return SchrittErgebnis.SpaeterErneut("KoSIT-Validator nicht erreichbar");
        }

        kontext.Kosit = ergebnis;
        kontext.VisualisierungHtml = html;
        await VisualisierungAblegenAsync(kontext.Beleg.Id, html, ct);
        return SchrittErgebnis.Weiter;
    }

    private async Task VisualisierungAblegenAsync(Guid belegId, string html, CancellationToken ct)
    {
        var daten = konfiguration["Pfade:Daten"]
            ?? throw new InvalidOperationException("Pfade:Daten ist nicht konfiguriert.");
        var relativ = Path.Combine("visualisierung", $"{belegId}.html");
        var absolut = Path.Combine(daten, relativ);
        Directory.CreateDirectory(Path.GetDirectoryName(absolut)!);
        var bytes = Encoding.UTF8.GetBytes(html);
        await File.WriteAllBytesAsync(absolut, bytes, ct);

        var datei = db.BelegDateien.Local.FirstOrDefault(d => d.BelegId == belegId && d.Rolle == DateiRolle.Visualisierung)
            ?? await db.BelegDateien.FirstOrDefaultAsync(d => d.BelegId == belegId && d.Rolle == DateiRolle.Visualisierung, ct);
        if (datei is null)
        {
            datei = new BelegDatei { Id = Guid.NewGuid(), BelegId = belegId, Rolle = DateiRolle.Visualisierung };
            db.BelegDateien.Add(datei);
        }

        datei.Pfad = relativ;
        datei.MimeType = "text/html";
        datei.Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        datei.Groesse = bytes.Length;
        datei.AbgelegtAm = zeit.GetUtcNow();
    }
}
