using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Eine archivierte Rechnung, Gutschrift oder Quittung.</summary>
public class Beleg
{
    public Guid Id { get; set; }
    public Richtung Richtung { get; set; }
    public Belegart Belegart { get; set; }
    public BelegStatus Status { get; set; }
    public ImportQuelle Quelle { get; set; }
    public Guid? ImportVorgangId { get; set; }
    public Guid? KorrespondentId { get; set; }
    public KorrespondentErkennung KorrespondentErkennung { get; set; }
    public Guid? KategorieId { get; set; }
    public bool KategorieManuell { get; set; }
    public string? Titel { get; set; }
    public string? Rechnungsnummer { get; set; }
    public DateOnly? Rechnungsdatum { get; set; }
    public DateOnly? Leistungsdatum { get; set; }
    public DateOnly? LeistungVon { get; set; }
    public DateOnly? LeistungBis { get; set; }
    public DateOnly? Faelligkeitsdatum { get; set; }
    public string Waehrung { get; set; } = "EUR";
    public decimal? Netto { get; set; }
    public decimal? Steuer { get; set; }
    public decimal? Brutto { get; set; }
    public ExtraktionsQuelle ExtraktionsQuelle { get; set; }

    /// <summary>Konfidenz der Document-Intelligence-Extraktion zwischen 0 und 1.</summary>
    public decimal? DiKonfidenz { get; set; }

    public DateTimeOffset? ExtrahiertAm { get; set; }
    public int Seitenanzahl { get; set; }
    public string? Volltext { get; set; }
    public Guid? DuplikatVonId { get; set; }
    public Guid? AufgeteiltAusBelegId { get; set; }
    public DateTimeOffset? BestaetigtAm { get; set; }
    public Guid? BestaetigtVonId { get; set; }
    public DateTimeOffset ErstelltAm { get; set; }
    public Guid? ErstelltVonId { get; set; }

    public Korrespondent? Korrespondent { get; set; }
    public Kategorie? Kategorie { get; set; }
    public List<BelegDatei> Dateien { get; set; } = [];
    public List<Steuerzeile> Steuerzeilen { get; set; } = [];
    public List<Position> Positionen { get; set; } = [];
    public List<BelegTag> Tags { get; set; } = [];
    public List<PruefBefund> Befunde { get; set; } = [];

    /// <summary>
    /// Letzter Tag der Aufbewahrungsfrist: 31.12. des Jahres (Belegjahr + <paramref name="jahre"/>).
    /// Belegjahr ist das Jahr des Rechnungsdatums, ersatzweise das des Imports.
    /// </summary>
    public DateOnly LoeschsperreBis(int jahre)
    {
        var belegjahr = Rechnungsdatum?.Year ?? ErstelltAm.UtcDateTime.Year;
        return new DateOnly(belegjahr + jahre, 12, 31);
    }
}
