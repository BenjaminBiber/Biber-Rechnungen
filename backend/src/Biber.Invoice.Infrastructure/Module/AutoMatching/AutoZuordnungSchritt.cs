using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.AutoMatching;

/// <summary>
/// Schritt 7b: ergänzt per ML-Modell, was die Regeln offengelassen haben – Kategorie (nicht manuell, ≥ 0,75),
/// Korrespondent (≥ 0,85) und Tags (≥ 0,75). Gesetzt werden nur Ziele, die aktuell den Algorithmus „Auto“ haben,
/// nie der System-Tag. Ohne Modell oder bei Fehlern läuft die Pipeline unverändert weiter.
/// </summary>
public sealed class AutoZuordnungSchritt(
    BiberDbContext db,
    AutoMatchingVorhersage vorhersage,
    TimeProvider zeit,
    ILogger<AutoZuordnungSchritt> log) : IPipelineSchritt
{
    public const double SchwelleKategorie = 0.75;
    public const double SchwelleKorrespondent = 0.85;
    public const double SchwelleTag = 0.75;

    public int Reihenfolge => SchrittReihenfolge.AutoZuordnung;

    public string Name => "Auto-Zuordnung";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        if (string.IsNullOrWhiteSpace(beleg.Volltext))
        {
            return SchrittErgebnis.Weiter;
        }

        try
        {
            if (await vorhersage.VorhersagenAsync(beleg.Volltext, ct) is { } ergebnis)
            {
                await AnwendenAsync(beleg, ergebnis, ct);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            log.LogWarning(e, "Auto-Zuordnung für Beleg {BelegId} fehlgeschlagen; der Schritt wird übersprungen.", beleg.Id);
        }

        return SchrittErgebnis.Weiter;
    }

    private async Task AnwendenAsync(Beleg beleg, AutoVorhersage ergebnis, CancellationToken ct)
    {
        if (beleg.KategorieId is null && !beleg.KategorieManuell
            && ergebnis.Kategorie is { Wahrscheinlichkeit: >= SchwelleKategorie } kategorie
            && await db.Kategorien.AnyAsync(k => k.Id == kategorie.Id && k.Matching.Algorithmus == MatchingAlgorithmus.Auto, ct))
        {
            beleg.KategorieId = kategorie.Id;
        }

        if (beleg.KorrespondentId is null
            && ergebnis.Korrespondent is { Wahrscheinlichkeit: >= SchwelleKorrespondent } korrespondent
            && await db.Korrespondenten.AnyAsync(k => k.Id == korrespondent.Id && k.Matching.Algorithmus == MatchingAlgorithmus.Auto, ct))
        {
            beleg.KorrespondentId = korrespondent.Id;
        }

        var kandidaten = ergebnis.Tags.Where(t => t.Wahrscheinlichkeit >= SchwelleTag).Select(t => t.Id).ToList();
        if (kandidaten.Count == 0)
        {
            return;
        }

        var erlaubt = await db.Tags
            .Where(t => kandidaten.Contains(t.Id) && !t.IstSystem && t.Matching.Algorithmus == MatchingAlgorithmus.Auto)
            .Select(t => t.Id)
            .ToListAsync(ct);
        var gesetzt = beleg.Tags.Select(t => t.TagId).ToHashSet();
        gesetzt.UnionWith(await db.BelegTags.Where(t => t.BelegId == beleg.Id).Select(t => t.TagId).ToListAsync(ct));
        foreach (var tagId in erlaubt.Where(id => !gesetzt.Contains(id)))
        {
            beleg.Tags.Add(new BelegTag { BelegId = beleg.Id, TagId = tagId, GesetztAm = zeit.GetUtcNow(), GesetztVonId = null });
        }
    }
}
