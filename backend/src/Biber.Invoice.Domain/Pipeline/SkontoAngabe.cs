namespace Biber.Invoice.Domain.Pipeline;

/// <summary>
/// Skonto, wie es auf dem Beleg steht: Prozentsatz und entweder eine Frist in Tagen ab Rechnungsdatum oder ein festes Datum.
/// </summary>
public sealed record SkontoAngabe(decimal Prozent, int? Tage, DateOnly? Bis);
