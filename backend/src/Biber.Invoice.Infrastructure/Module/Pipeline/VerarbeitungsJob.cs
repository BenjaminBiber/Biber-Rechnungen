using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>
/// Orchestrator: führt alle <see cref="IPipelineSchritt"/>e eines Import-Vorgangs aufsteigend nach
/// <see cref="IPipelineSchritt.Reihenfolge"/> aus. Ein neuer Versuch führt immer <b>alle</b> Schritte erneut aus.
/// </summary>
public sealed class VerarbeitungsJob(
    BiberDbContext db,
    IEnumerable<IPipelineSchritt> schritte,
    IVerarbeitungsPlaner planer,
    IEnumerable<IBenachrichtigungsDienst> benachrichtigungen,
    TimeProvider zeit,
    ILogger<VerarbeitungsJob> log)
{
    public const int MaxVersuche = 5;

    public const string SchrittOffen = "offen";
    public const string SchrittLaeuft = "laeuft";
    public const string SchrittOk = "ok";
    public const string SchrittFehler = "fehler";

    /// <summary>Wartezeit nach dem n-ten Fehlschlag (1, 4, 10, 20, 30 Minuten; zusammen ca. 1 h).</summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(4),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(20),
        TimeSpan.FromMinutes(30),
    ];

    /// <summary>Wartezeit nach dem <paramref name="versuch"/>-ten Fehlschlag (ab 1).</summary>
    public static TimeSpan Wartezeit(int versuch) => Backoff[Math.Clamp(versuch, 1, Backoff.Length) - 1];

    [Queue(PipelineModul.QueueVerarbeitung)]
    [AutomaticRetry(Attempts = 0)] // Wiederholungen steuert der Job selbst (Backoff, Status „Wartet“)
    public async Task AusfuehrenAsync(Guid importVorgangId, CancellationToken ct)
    {
        var vorgang = await db.ImportVorgaenge.FirstOrDefaultAsync(v => v.Id == importVorgangId, ct);
        if (vorgang is null)
        {
            log.LogWarning("Import-Vorgang {Vorgang} nicht gefunden", importVorgangId);
            return;
        }

        if (vorgang.Status is not (ImportStatus.Angenommen or ImportStatus.Wartet or ImportStatus.Pausiert or ImportStatus.InVerarbeitung))
        {
            log.LogInformation("Import-Vorgang {Vorgang} ist bereits abgeschlossen ({Status})", vorgang.Id, vorgang.Status);
            return;
        }

        var beleg = await BelegLadenOderAnlegenAsync(vorgang, ct);
        var reihe = schritte.OrderBy(s => s.Reihenfolge).ToList();
        vorgang.Status = ImportStatus.InVerarbeitung;
        vorgang.NaechsterVersuchAm = null;
        vorgang.Schritte = [.. reihe.Select(s => new ImportSchritt { Name = s.Name, Status = SchrittOffen })];
        beleg.Status = BelegStatus.InVerarbeitung;
        await db.SaveChangesAsync(ct);

        var kontext = new VerarbeitungsKontext
        {
            Vorgang = vorgang,
            Beleg = beleg,
            DateiPfad = vorgang.StagingPfad ?? "",
            Dateiname = vorgang.Dateiname,
        };

        for (var i = 0; i < reihe.Count; i++)
        {
            var schritt = reihe[i];
            vorgang.Schritte[i].Status = SchrittLaeuft;
            await db.SaveChangesAsync(ct);

            SchrittErgebnis ergebnis;
            try
            {
                ergebnis = await schritt.AusfuehrenAsync(kontext, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                log.LogWarning(ex, "Schritt {Schritt} für Import-Vorgang {Vorgang} fehlgeschlagen", schritt.Name, vorgang.Id);

                // Halbfertige Änderungen des Schritts verwerfen und mit frischem Stand weitermachen.
                db.ChangeTracker.Clear();
                vorgang = await db.ImportVorgaenge.FirstAsync(v => v.Id == importVorgangId, ct);
                beleg = await db.Belege.FirstAsync(b => b.Id == beleg.Id, ct);
                vorgang.Schritte[i].Status = SchrittFehler;
                await FehlschlagAsync(vorgang, beleg, $"{schritt.Name}: {ex.Message}", null, ct);
                return;
            }

            switch (ergebnis.Ausgang)
            {
                case SchrittAusgang.Weiter:
                    vorgang.Schritte[i].Status = SchrittOk;
                    await db.SaveChangesAsync(ct);
                    break;

                case SchrittAusgang.Beendet:
                    vorgang.Schritte[i].Status = SchrittOk;
                    vorgang.Status = ergebnis.Endstatus ?? ImportStatus.Fertig;
                    vorgang.Ergebnis = ergebnis.Text;
                    vorgang.BeendetAm = zeit.GetUtcNow();
                    if (vorgang.Status == ImportStatus.Fertig)
                    {
                        beleg.Status = BelegStatus.Fertig;
                    }

                    await db.SaveChangesAsync(ct);
                    return;

                case SchrittAusgang.SpaeterErneut:
                    vorgang.Schritte[i].Status = SchrittFehler;
                    await FehlschlagAsync(vorgang, beleg, ergebnis.Text ?? schritt.Name, ergebnis.Nach, ct);
                    return;

                case SchrittAusgang.Pausiert:
                    vorgang.Schritte[i].Status = SchrittOffen;
                    vorgang.Status = ImportStatus.Pausiert;
                    vorgang.Ergebnis = ergebnis.Text;
                    await db.SaveChangesAsync(ct);
                    return;

                default:
                    throw new InvalidOperationException($"Unbekannter Ausgang {ergebnis.Ausgang} von Schritt {schritt.Name}.");
            }
        }

        beleg.Status = BelegStatus.Fertig;
        vorgang.Status = ImportStatus.Fertig;
        vorgang.Ergebnis = "1 Beleg angelegt";
        vorgang.Fehler = null;
        vorgang.BeendetAm = zeit.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Neuer Beleg zu einem Import-Vorgang (noch nicht gespeichert).</summary>
    internal static Beleg NeuerBeleg(ImportVorgang vorgang, DateTimeOffset jetzt) => new()
    {
        Id = Guid.NewGuid(),
        Richtung = Richtung.Eingang,
        Belegart = Belegart.Rechnung,
        Status = BelegStatus.InVerarbeitung,
        Quelle = vorgang.Quelle,
        ImportVorgangId = vorgang.Id,
        ErstelltVonId = vorgang.BenutzerId,
        ErstelltAm = jetzt,
    };

    private async Task<Beleg> BelegLadenOderAnlegenAsync(ImportVorgang vorgang, CancellationToken ct)
    {
        if (vorgang.BelegId is { } belegId)
        {
            var vorhanden = await db.Belege
                .Include(b => b.Steuerzeilen)
                .Include(b => b.Positionen)
                .FirstOrDefaultAsync(b => b.Id == belegId, ct);
            if (vorhanden is not null)
            {
                return vorhanden;
            }
        }

        var beleg = NeuerBeleg(vorgang, zeit.GetUtcNow());
        db.Belege.Add(beleg);
        vorgang.BelegId = beleg.Id;
        return beleg;
    }

    /// <summary>
    /// Zählt einen Versuch. Unter <see cref="MaxVersuche"/>: Status „Wartet“ und neu einplanen;
    /// beim letzten Versuch: Vorgang und Beleg „Fehler“, Tag „zu prüfen“, Meldung.
    /// </summary>
    private async Task FehlschlagAsync(ImportVorgang vorgang, Beleg beleg, string grund, TimeSpan? nach, CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        vorgang.Versuche++;
        vorgang.Ergebnis = grund;

        if (vorgang.Versuche < MaxVersuche)
        {
            var wartezeit = nach ?? Wartezeit(vorgang.Versuche);
            vorgang.Status = ImportStatus.Wartet;
            vorgang.NaechsterVersuchAm = jetzt + wartezeit;
            await db.SaveChangesAsync(ct);
            planer.Einplanen(vorgang.Id, wartezeit);
            return;
        }

        vorgang.Status = ImportStatus.Fehler;
        vorgang.Fehler = grund;
        vorgang.NaechsterVersuchAm = null;
        vorgang.BeendetAm = jetzt;
        beleg.Status = BelegStatus.Fehler;
        if (!await db.BelegTags.AnyAsync(t => t.BelegId == beleg.Id && t.TagId == SystemIds.TagZuPruefen, ct))
        {
            db.BelegTags.Add(new BelegTag { BelegId = beleg.Id, TagId = SystemIds.TagZuPruefen, GesetztAm = jetzt });
        }

        await db.SaveChangesAsync(ct);
        log.LogWarning("Import-Vorgang {Vorgang} nach {Versuche} Versuchen fehlgeschlagen", vorgang.Id, vorgang.Versuche);
        await benachrichtigungen.MeldenAsync(
            new NeueBenachrichtigung(
                BenachrichtigungArt.ImportFehler,
                Schweregrad.Fehler,
                "Import fehlgeschlagen",
                $"{vorgang.Dateiname}: {grund}",
                Link: $"/belege/{beleg.Id}"),
            ct);
    }
}
