using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.Domain.Pipeline;

/// <param name="Artikelnummer">Artikelnummer des Lieferanten, falls auf dem Beleg.</param>
/// <param name="Merkmale">Größe, Farbe o. Ä., falls getrennt von der Bezeichnung angegeben; <c>null</c> ohne.</param>
public sealed record ExtrahiertePosition(
    int Nr,
    string Text,
    decimal? Menge,
    string? Einheit,
    decimal? Einzelpreis,
    decimal? Gesamt,
    decimal? Steuersatz,
    string? Artikelnummer = null,
    IReadOnlyList<Merkmal>? Merkmale = null);
