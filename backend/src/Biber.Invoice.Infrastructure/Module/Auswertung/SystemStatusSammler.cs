using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Auswertung;
using Biber.Invoice.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Auswertung;

/// <summary>
/// Sammelt die Einträge aller registrierten <see cref="ISystemStatusQuelle"/>n, sortiert nach Schlüssel.
/// Wirft eine Quelle, steht für sie ein Eintrag mit Schweregrad <see cref="Schweregrad.Fehler"/> in der Liste.
/// </summary>
public sealed class SystemStatusSammler(
    IEnumerable<ISystemStatusQuelle> quellen,
    TimeProvider zeit,
    ILogger<SystemStatusSammler> logger)
{
    public async Task<SystemStatusEintragDto[]> LesenAsync(CancellationToken ct)
    {
        var eintraege = new List<SystemStatusEintragDto>();
        foreach (var quelle in quellen)
        {
            eintraege.Add(await LesenAsync(quelle, ct));
        }

        return [.. eintraege.OrderBy(e => e.Schluessel, StringComparer.Ordinal)];
    }

    private async Task<SystemStatusEintragDto> LesenAsync(ISystemStatusQuelle quelle, CancellationToken ct)
    {
        try
        {
            return await quelle.LesenAsync(ct);
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(fehler, "Systemstatus: Quelle {Schluessel} ist fehlgeschlagen.", quelle.Schluessel);
            return new SystemStatusEintragDto(
                quelle.Schluessel,
                Schweregrad.Fehler,
                "Status nicht verfügbar",
                "Der Status konnte nicht ermittelt werden.",
                zeit.GetUtcNow());
        }
    }
}
