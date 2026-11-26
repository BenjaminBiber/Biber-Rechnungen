using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Belege;

/// <summary>
/// Löscht einen Beleg trotz Löschsperre (nur Admin). <see cref="Grund"/> muss angegeben sein; der Beleg wird nur
/// ausgeblendet und kann unter „Gelöschte Belege“ wiederhergestellt werden.
/// </summary>
public sealed record BelegLoeschenDto(LoeschGrund? Grund, string? Bemerkung);

/// <summary>Ein gelöschter (falsch zugeordneter) Beleg in der Liste „Gelöschte Belege“.</summary>
public sealed record GeloeschterBelegDto(
    Guid Id,
    string? Titel,
    string? Rechnungsnummer,
    DateOnly? Rechnungsdatum,
    string? Korrespondent,
    decimal? Brutto,
    string Waehrung,
    LoeschGrund Grund,
    string? Bemerkung,
    DateTimeOffset GeloeschtAm,
    string? GeloeschtVon,
    Guid? OriginalId);
