using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Positionen;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>
/// „Erneut auslesen“: führt für einen abgelegten Beleg die Schritte ab <see cref="SchrittReihenfolge.Formaterkennung"/>
/// mit dem Original aus der Ablage aus, direkt im Aufruf (nicht über Hangfire). Die Positionen werden dabei neu
/// angelegt und zugeordnet; Artikel, die dadurch keine Position mehr haben, werden danach entfernt.
/// </summary>
public sealed class VerarbeitungsStarter(
    BiberDbContext db,
    IEnumerable<IPipelineSchritt> schritte,
    IConfiguration konfiguration,
    TimeProvider zeit) : IVerarbeitungsStarter
{
    public async Task ErneutVerarbeitenAsync(Guid belegId, CancellationToken ct)
    {
        var beleg = await db.Belege
            .Include(b => b.Dateien)
            .Include(b => b.Steuerzeilen)
            .Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == belegId, ct)
            ?? throw new KeyNotFoundException($"Beleg {belegId} nicht gefunden.");
        var original = beleg.Dateien.FirstOrDefault(d => d.Rolle == DateiRolle.Original)
            ?? throw new InvalidOperationException($"Beleg {belegId} hat keine Originaldatei.");
        var ablage = konfiguration["Pfade:Ablage"]
            ?? throw new InvalidOperationException("Pfade:Ablage ist nicht konfiguriert.");

        // Ohne zugehörigen Import-Vorgang (z. B. nach dessen Löschung) arbeiten die Schritte mit einem flüchtigen Ersatz.
        var vorgang = beleg.ImportVorgangId is { } vorgangId
            ? await db.ImportVorgaenge.FirstOrDefaultAsync(v => v.Id == vorgangId, ct)
            : null;
        vorgang ??= new ImportVorgang
        {
            Id = Guid.NewGuid(),
            Quelle = beleg.Quelle,
            Status = ImportStatus.InVerarbeitung,
            Dateiname = Path.GetFileName(original.Pfad),
            Groesse = original.Groesse,
            Sha256 = original.Sha256,
            BelegId = beleg.Id,
            ErstelltAm = zeit.GetUtcNow(),
        };

        var bisherigeArtikel = VerwaisteArtikel.Kandidaten(beleg.Positionen);
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = vorgang,
            Beleg = beleg,
            DateiPfad = Path.GetFullPath(Path.Combine(ablage, original.Pfad)),
            Dateiname = Path.GetFileName(original.Pfad),
            IstWiederholung = true,
        };

        foreach (var schritt in schritte.Where(s => s.Reihenfolge >= SchrittReihenfolge.Formaterkennung).OrderBy(s => s.Reihenfolge))
        {
            var ergebnis = await schritt.AusfuehrenAsync(kontext, ct);
            await db.SaveChangesAsync(ct);
            switch (ergebnis.Ausgang)
            {
                case SchrittAusgang.Weiter:
                    continue;
                case SchrittAusgang.Beendet:
                    await VerwaisteArtikel.EntfernenAsync(db, bisherigeArtikel, ct);
                    return;
                default:
                    throw new VerarbeitungAbgebrochenException(schritt.Name, ergebnis.Ausgang, ergebnis.Text);
            }
        }

        beleg.Status = BelegStatus.Fertig;
        await db.SaveChangesAsync(ct);
        await VerwaisteArtikel.EntfernenAsync(db, bisherigeArtikel, ct);
    }
}
