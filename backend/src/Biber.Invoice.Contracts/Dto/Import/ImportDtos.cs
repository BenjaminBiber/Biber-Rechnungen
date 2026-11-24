using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Import;

public sealed record ImportVorgangDto(
    Guid Id,
    DateTimeOffset Zeitpunkt,
    ImportQuelle Quelle,
    string Dateiname,
    long Groesse,
    string Herkunft,
    string? HerkunftDetail,
    ImportStatus Status,
    int Versuche,
    int MaxVersuche,
    string? Ergebnis,
    string? Fehler,
    Guid? BelegId,
    Guid? DuplikatVonBelegId,
    DateTimeOffset? NaechsterVersuchAm,
    ImportSchrittDto[] Schritte);

/// <summary>Ein Pipeline-Schritt; <see cref="Status"/>: „offen“, „laeuft“, „ok“ oder „fehler“.</summary>
public sealed record ImportSchrittDto(string Name, string Status);
