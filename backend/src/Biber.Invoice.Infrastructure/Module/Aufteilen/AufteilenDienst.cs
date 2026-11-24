using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Aufteilen;

/// <summary>
/// Teilt einen mehrseitigen PDF-Beleg nach Seiten in neue Belege. Das Original wird nur gelesen;
/// jeder Teil läuft als eigener Import durch die Pipeline und ist über <c>AufgeteiltAusBelegId</c> verknüpft.
/// </summary>
public sealed class AufteilenDienst(
    BiberDbContext db,
    IImportDienst import,
    IPdfTeiler teiler,
    IAktuellerBenutzer benutzer,
    IConfiguration konfiguration,
    TimeProvider zeit,
    ILogger<AufteilenDienst> log) : IAufteilenDienst
{
    public const string AuditAktion = "Aufgeteilt";

    public async Task<IReadOnlyList<ImportVorgangDto>?> AufteilenAsync(Guid belegId, AufteilenDto anfrage, CancellationToken ct)
    {
        var beleg = await db.Belege.AsNoTracking().FirstOrDefaultAsync(b => b.Id == belegId, ct);
        if (beleg is null)
        {
            return null;
        }

        var original = await db.BelegDateien.AsNoTracking()
            .FirstOrDefaultAsync(d => d.BelegId == belegId && d.Rolle == DateiRolle.Original, ct);
        if (original is null || original.MimeType != "application/pdf")
        {
            throw new AufteilenKonfliktAusnahme("Nur PDF-Belege lassen sich aufteilen.");
        }

        if (await db.Belege.AnyAsync(b => b.AufgeteiltAusBelegId == belegId, ct))
        {
            throw new AufteilenKonfliktAusnahme("Der Beleg wurde bereits aufgeteilt.");
        }

        PruefeTeile(anfrage, beleg.Seitenanzahl);

        var originalname = await db.ImportVorgaenge.AsNoTracking()
            .Where(v => v.Id == beleg.ImportVorgangId)
            .Select(v => v.Dateiname)
            .FirstOrDefaultAsync(ct) ?? Path.GetFileName(original.Pfad);
        var basisname = Path.GetFileNameWithoutExtension(originalname);

        var ablage = konfiguration["Pfade:Ablage"]
            ?? throw new InvalidOperationException("Pfade:Ablage ist nicht konfiguriert.");
        var pfad = Path.Combine(ablage, original.Pfad);
        var teile = new List<byte[]>(anfrage.Teile.Length);
        await using (var strom = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using var puffer = new MemoryStream();
            await strom.CopyToAsync(puffer, ct);
            foreach (var teil in anfrage.Teile)
            {
                puffer.Position = 0;
                teile.Add(teiler.Extrahieren(puffer, teil.Seiten));
            }
        }

        var vorgaenge = new List<ImportVorgangDto>(teile.Count);
        for (var i = 0; i < teile.Count; i++)
        {
            var n = i + 1;
            using var inhalt = new MemoryStream(teile[i]);
            vorgaenge.Add(await import.AnnehmenAsync(
                inhalt,
                $"{basisname}_Teil{n}.pdf",
                beleg.Quelle,
                new ImportHerkunft(Pfad: $"Teil {n} von {originalname}", BenutzerId: benutzer.Id, AufgeteiltAusBelegId: belegId),
                ct));
        }

        db.AuditEintraege.Add(new AuditEintrag
        {
            Zeitpunkt = zeit.GetUtcNow(),
            BenutzerId = benutzer.Id,
            BenutzerName = benutzer.IstSystem || benutzer.Id is null ? "System" : benutzer.Anzeigename,
            Entitaet = nameof(Beleg),
            EntitaetId = belegId.ToString(),
            BelegId = belegId,
            Aktion = AuditAktion,
            NeuerWert = $"Aufgeteilt in {teile.Count} Belege",
        });
        await db.SaveChangesAsync(ct);

        log.LogInformation("Beleg {Beleg} in {Anzahl} Belege aufgeteilt", belegId, teile.Count);
        return vorgaenge;
    }

    private static void PruefeTeile(AufteilenDto anfrage, int seitenanzahl)
    {
        var teile = anfrage.Teile ?? [];
        if (teile.Length < 2)
        {
            throw AufteilenValidierungsAusnahme.Fuer("teile", "Es sind mindestens zwei Teile nötig.");
        }

        if (teile.Any(t => t.Seiten is null || t.Seiten.Length == 0))
        {
            throw AufteilenValidierungsAusnahme.Fuer("teile", "Kein Teil darf leer sein.");
        }

        var seiten = teile.SelectMany(t => t.Seiten).ToList();
        if (seiten.Where(s => s < 1 || s > seitenanzahl).Select(s => (int?)s).FirstOrDefault() is { } ausserhalb)
        {
            throw AufteilenValidierungsAusnahme.Fuer("teile", $"Seite {ausserhalb} gibt es nicht; der Beleg hat {seitenanzahl} Seiten.");
        }

        if (seiten.GroupBy(s => s).FirstOrDefault(g => g.Count() > 1) is { } doppelt)
        {
            throw AufteilenValidierungsAusnahme.Fuer("teile", $"Seite {doppelt.Key} kommt in mehreren Teilen vor.");
        }

        if (Enumerable.Range(1, seitenanzahl).FirstOrDefault(s => !seiten.Contains(s)) is var fehlt and not 0)
        {
            throw AufteilenValidierungsAusnahme.Fuer("teile", $"Seite {fehlt} ist keinem Teil zugeordnet.");
        }
    }
}
