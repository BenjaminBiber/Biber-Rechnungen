using System.Globalization;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Audit;
using Biber.Invoice.Infrastructure.Module.Positionen;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Aufbewahrung;

/// <summary>
/// Löschbare Jahre und Löschung der App-Daten nach Ablauf der Aufbewahrungsfrist. Dateien unter
/// <c>Pfade:Ablage</c> bleiben unberührt; ihre Pfade gehen an den Admin.
/// </summary>
public sealed class AufbewahrungsDienst(
    BiberDbContext db,
    IEinstellungen einstellungen,
    IAufbewahrungsFreigabe freigabe,
    IAuditSchreiber audit,
    IConfiguration konfiguration,
    TimeProvider zeit,
    ILogger<AufbewahrungsDienst> logger)
{
    public const string Bestaetigungswort = "LÖSCHEN";

    public async Task<LoeschbarDto[]> LoeschbarAsync(CancellationToken ct)
    {
        var (jahre, heute) = await FristAsync(ct);
        var kandidaten = await db.Belege.AsNoTracking()
            .Select(b => new Beleg { Id = b.Id, Rechnungsdatum = b.Rechnungsdatum, ErstelltAm = b.ErstelltAm })
            .ToListAsync(ct);

        return kandidaten
            .Select(b => (Jahr: Belegjahr(b), FristEnde: b.LoeschsperreBis(jahre)))
            .Where(b => b.FristEnde < heute)
            .GroupBy(b => b.Jahr)
            .OrderBy(g => g.Key)
            .Select(g => new LoeschbarDto(g.Key, g.Count(), g.First().FristEnde))
            .ToArray();
    }

    /// <summary>Löscht alle löschbaren Belege des Jahres; <c>null</c>, wenn es dort nichts Löschbares gibt.</summary>
    public async Task<LoeschErgebnisDto?> LoeschenAsync(int jahr, CancellationToken ct)
    {
        var (jahre, heute) = await FristAsync(ct);
        var belege = (await db.Belege
                .Where(b => b.Rechnungsdatum == null || b.Rechnungsdatum.Value.Year == jahr)
                .Include(b => b.Korrespondent)
                .Include(b => b.Dateien)
                .Include(b => b.Positionen)
                .Include(b => b.Steuerzeilen)
                .Include(b => b.Tags)
                .Include(b => b.Befunde)
                .AsSplitQuery()
                .ToListAsync(ct))
            .Where(b => Belegjahr(b) == jahr && b.LoeschsperreBis(jahre) < heute)
            .ToList();
        if (belege.Count == 0)
        {
            return null;
        }

        var ids = belege.Select(b => b.Id).ToList();
        var abgeleitet = new List<string>();
        var pfade = new List<string>();
        foreach (var beleg in belege)
        {
            var dateien = beleg.Dateien.OrderBy(d => d.Rolle).ThenBy(d => d.Pfad, StringComparer.Ordinal).ToList();
            var ablage = dateien.Where(d => d.Rolle is DateiRolle.Original or DateiRolle.EingebettetesXml)
                .Select(d => d.Pfad).ToList();
            pfade.AddRange(ablage);
            abgeleitet.AddRange(dateien.Where(d => d.Rolle is DateiRolle.Vorschau or DateiRolle.Visualisierung)
                .Select(d => d.Pfad));
            audit.Notieren(nameof(Beleg), beleg.Id.ToString(), beleg.Id, AuditInterceptor.Geloescht,
                Beschreibung(beleg, ablage));
        }

        var vorgaenge = await db.ImportVorgaenge.Where(v => v.BelegId != null && ids.Contains(v.BelegId.Value))
            .ToListAsync(ct);
        foreach (var vorgang in vorgaenge)
        {
            vorgang.BelegId = null;
        }

        var artikel = VerwaisteArtikel.Kandidaten(belege.SelectMany(b => b.Positionen));
        freigabe.Freigeben(ids);
        db.Belege.RemoveRange(belege);
        await db.SaveChangesAsync(ct);
        await VerwaisteArtikel.EntfernenAsync(db, artikel, ct);

        AbgeleiteteLoeschen(abgeleitet);
        logger.LogInformation("Aufbewahrung: {Anzahl} Belege aus {Jahr} gelöscht.", belege.Count, jahr);
        return new LoeschErgebnisDto(belege.Count, [.. pfade]);
    }

    private async Task<(int Jahre, DateOnly Heute)> FristAsync(CancellationToken ct)
    {
        var aufbewahrung = await einstellungen.LesenAsync<AufbewahrungEinstellungen>(ct);
        var allgemein = await einstellungen.LesenAsync<AllgemeinEinstellungen>(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(allgemein.Zeitzone);
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), zone).DateTime);
        return (aufbewahrung.Jahre, heute);
    }

    private static int Belegjahr(Beleg beleg) => beleg.LoeschsperreBis(0).Year;

    private static string Beschreibung(Beleg beleg, IEnumerable<string> ablage) => string.Join("; ",
        $"Rechnungsnummer: {beleg.Rechnungsnummer}",
        $"Korrespondent: {beleg.Korrespondent?.Name}",
        $"Datum: {beleg.Rechnungsdatum?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
        $"Brutto: {beleg.Brutto?.ToString("0.00", CultureInfo.InvariantCulture)}",
        $"Ablage: {string.Join(", ", ablage)}");

    /// <summary>Löscht Vorschau und Visualisierung unter <c>Pfade:Daten</c>; nie etwas außerhalb davon.</summary>
    private void AbgeleiteteLoeschen(IEnumerable<string> relativ)
    {
        var daten = konfiguration["Pfade:Daten"];
        if (string.IsNullOrEmpty(daten))
        {
            return;
        }

        var wurzel = Path.GetFullPath(daten);
        var praefix = Path.EndsInDirectorySeparator(wurzel) ? wurzel : wurzel + Path.DirectorySeparatorChar;
        var vergleich = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var pfad in relativ)
        {
            var absolut = Path.GetFullPath(Path.Combine(wurzel, pfad));
            if (!absolut.StartsWith(praefix, vergleich))
            {
                logger.LogWarning("Aufbewahrung: Pfad {Pfad} liegt außerhalb von Pfade:Daten und bleibt liegen.", pfad);
                continue;
            }

            try
            {
                File.Delete(absolut);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(e, "Aufbewahrung: Abgeleitete Datei {Pfad} ließ sich nicht löschen.", pfad);
            }
        }
    }
}
