using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Ausgelesene Daten eines Belegs, unabhängig von der Quelle (E-Rechnung oder Document Intelligence).</summary>
public sealed record ExtraktionsErgebnis
{
    public ExtraktionsQuelle Quelle { get; init; }
    public Belegart Belegart { get; init; }
    public string? Rechnungsnummer { get; init; }
    public DateOnly? Rechnungsdatum { get; init; }
    public DateOnly? Leistungsdatum { get; init; }
    public DateOnly? LeistungVon { get; init; }
    public DateOnly? LeistungBis { get; init; }
    public DateOnly? Faelligkeitsdatum { get; init; }
    public string Waehrung { get; init; } = "EUR";
    public decimal? Netto { get; init; }
    public decimal? Steuer { get; init; }
    public decimal? Brutto { get; init; }
    public IReadOnlyList<ExtrahierteSteuerzeile> Steuerzeilen { get; init; } = [];
    public IReadOnlyList<ExtrahiertePosition> Positionen { get; init; } = [];
    public ExtrahiertePartei? Aussteller { get; init; }
    public ExtrahiertePartei? Empfaenger { get; init; }
    public string? Volltext { get; init; }

    /// <summary>Strukturiert angegebenes Skonto (E-Rechnung); sonst wird es später aus dem Volltext gelesen.</summary>
    public SkontoAngabe? Skonto { get; init; }

    /// <summary>Bei einer Gutschrift: Nummer der Rechnung, auf die sie sich bezieht (BT-25).</summary>
    public string? BezugRechnungsnummer { get; init; }

    /// <summary>Konfidenz zwischen 0 und 1; nur bei Document Intelligence.</summary>
    public decimal? Konfidenz { get; init; }

    public int Seiten { get; init; }
}
