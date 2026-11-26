namespace Biber.Invoice.Domain.Pipeline;

/// <param name="Artikelnummer">Artikelnummer des Lieferanten, falls auf dem Beleg.</param>
/// <param name="Variante">Größe, Farbe o. Ä., falls getrennt von der Bezeichnung angegeben.</param>
public sealed record ExtrahiertePosition(
    int Nr,
    string Text,
    decimal? Menge,
    string? Einheit,
    decimal? Einzelpreis,
    decimal? Gesamt,
    decimal? Steuersatz,
    string? Artikelnummer = null,
    string? Variante = null);
