namespace Biber.Invoice.Domain.Pipeline;

public sealed record ExtrahiertePosition(
    int Nr,
    string Text,
    decimal? Menge,
    string? Einheit,
    decimal? Einzelpreis,
    decimal? Gesamt,
    decimal? Steuersatz);
