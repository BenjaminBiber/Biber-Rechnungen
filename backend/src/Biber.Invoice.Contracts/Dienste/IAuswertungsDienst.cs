using Biber.Invoice.Contracts.Dto.Auswertung;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Auswertungen; Zeitbasis ist immer das Rechnungsdatum.</summary>
public interface IAuswertungsDienst
{
    /// <param name="zeitraum">„Monat“, „Quartal“ oder „Jahr“.</param>
    Task<DashboardDto> DashboardAsync(string zeitraum, int jahr, CancellationToken ct);

    Task<KostenAuswertungDto> KostenAsync(DateOnly von, DateOnly bis, KostenGruppierung gruppierung, CancellationToken ct);
}
