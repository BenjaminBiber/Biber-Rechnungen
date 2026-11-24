using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Gemeinsamer Eingang der Pipeline für Upload, Ordner, Mail und MCP.</summary>
public interface IImportDienst
{
    /// <summary>Legt einen Import-Vorgang an, staged die Datei und plant die Verarbeitung ein.</summary>
    Task<ImportVorgangDto> AnnehmenAsync(Stream inhalt, string dateiname, ImportQuelle quelle, ImportHerkunft herkunft, CancellationToken ct);

    /// <param name="quelle"><c>null</c> = alle Quellen.</param>
    /// <param name="tage">Nur Vorgänge der letzten <paramref name="tage"/> Tage.</param>
    Task<SeiteDto<ImportVorgangDto>> VorgaengeAsync(ImportQuelle? quelle, int tage, int seite, CancellationToken ct);

    /// <returns><c>null</c>, wenn es den Vorgang nicht gibt.</returns>
    Task<ImportVorgangDto?> VorgangAsync(Guid id, CancellationToken ct);

    /// <summary>Setzt wegen des DI-Limits pausierte Vorgänge fort.</summary>
    /// <returns>Anzahl der fortgesetzten Vorgänge.</returns>
    Task<int> PausierteFortsetzenAsync(CancellationToken ct);
}

/// <summary>Woher eine Datei kam; gesetzt sind nur die Angaben der jeweiligen Quelle.</summary>
public sealed record ImportHerkunft(
    string? Pfad = null,
    string? MailMessageId = null,
    string? MailAbsender = null,
    string? MailBetreff = null,
    Guid? BenutzerId = null,
    Guid? ApiTokenId = null,
    Guid? AufgeteiltAusBelegId = null);
