using Biber.Invoice.Contracts.Dto.Export;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Export;

/// <summary>Die Belege eines Exports und die Zahlen dazu.</summary>
/// <param name="Belege">Ausgewählte Belege, sortiert nach Rechnungsdatum, Korrespondent und Rechnungsnummer.</param>
/// <param name="DublettenAusgeschlossen">Wegen „Dubletten ausschließen“ weggelassene Belege.</param>
/// <param name="AnzahlZuPruefen">Belege mit „zu prüfen“ im Zeitraum und in den Filtern, ausgeschlossen oder nicht.</param>
public sealed record ExportAuswahlErgebnis(IReadOnlyList<Beleg> Belege, int DublettenAusgeschlossen, int AnzahlZuPruefen)
{
    public const string Euro = "EUR";

    /// <summary>Belege in Euro; nur sie zählen in Summen.</summary>
    public IEnumerable<Beleg> InEuro => Belege.Where(IstEuro);

    public static bool IstEuro(Beleg beleg) => string.Equals(beleg.Waehrung, Euro, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Wählt die Belege eines Exports: Status <see cref="BelegStatus.Fertig"/>, Rechnungsdatum in [Von, Bis],
/// gefiltert nach Kategorien, Korrespondenten und Tags (mindestens einer der gewählten).
/// </summary>
public static class ExportAuswahl
{
    public static async Task<ExportAuswahlErgebnis> LadenAsync(BiberDbContext db, ExportAnfrageDto anfrage, CancellationToken ct)
    {
        var abfrage = db.Belege.AsNoTracking()
            .Where(b => b.Status == BelegStatus.Fertig
                && b.Rechnungsdatum != null
                && b.Rechnungsdatum >= anfrage.Von
                && b.Rechnungsdatum <= anfrage.Bis);

        if (anfrage.KategorieIds is { Length: > 0 } kategorien)
        {
            // Eine Oberkategorie schließt ihre Unterkategorien ein.
            abfrage = abfrage.Where(b => b.KategorieId != null
                && (kategorien.Contains(b.KategorieId.Value)
                    || (b.Kategorie!.ElternId != null && kategorien.Contains(b.Kategorie.ElternId.Value))));
        }

        if (anfrage.KorrespondentIds is { Length: > 0 } korrespondenten)
        {
            abfrage = abfrage.Where(b => b.KorrespondentId != null && korrespondenten.Contains(b.KorrespondentId.Value));
        }

        if (anfrage.TagIds is { Length: > 0 } tags)
        {
            abfrage = abfrage.Where(b => b.Tags.Any(t => tags.Contains(t.TagId)));
        }

        var kandidaten = await abfrage
            .Include(b => b.Korrespondent)
            .Include(b => b.Kategorie).ThenInclude(k => k!.Eltern)
            .Include(b => b.Steuerzeilen)
            .Include(b => b.Dateien)
            .Include(b => b.Tags).ThenInclude(t => t.Tag)
            .AsSplitQuery()
            .ToListAsync(ct);

        var anzahlZuPruefen = kandidaten.Count(IstZuPruefen);
        var dubletten = anfrage.DublettenAusschliessen ? kandidaten.Count(b => b.DuplikatVonId != null) : 0;
        var belege = kandidaten
            .Where(b => !anfrage.DublettenAusschliessen || b.DuplikatVonId == null)
            .Where(b => !anfrage.ZuPruefenAusschliessen || !IstZuPruefen(b))
            .OrderBy(b => b.Rechnungsdatum)
            .ThenBy(b => b.Korrespondent?.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.Rechnungsnummer, StringComparer.Ordinal)
            .ThenBy(b => b.Id)
            .ToList();

        return new ExportAuswahlErgebnis(belege, dubletten, anzahlZuPruefen);
    }

    private static bool IstZuPruefen(Beleg beleg) => beleg.Tags.Any(t => t.TagId == SystemIds.TagZuPruefen);
}
