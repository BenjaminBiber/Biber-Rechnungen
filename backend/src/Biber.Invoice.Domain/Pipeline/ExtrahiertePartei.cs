using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Aussteller oder Empfänger, wie auf dem Beleg angegeben.</summary>
public sealed record ExtrahiertePartei(
    string? Name,
    string? Strasse,
    string? Plz,
    string? Ort,
    string? Land,
    string? UstIdNr,
    string? Steuernummer,
    IReadOnlyList<Bankverbindung> Bankverbindungen,
    string? Email,
    string? Website);
