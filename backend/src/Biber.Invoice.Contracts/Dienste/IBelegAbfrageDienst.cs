using Biber.Invoice.Contracts.Dto.Belege;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Lesender Zugriff auf Belege für REST-API und MCP.</summary>
public interface IBelegAbfrageDienst
{
    Task<BelegListeDto> SuchenAsync(BelegFilterDto filter, CancellationToken ct);

    Task<BelegZaehlerDto> ZaehlerAsync(CancellationToken ct);

    /// <returns><c>null</c>, wenn es den Beleg nicht gibt.</returns>
    Task<BelegDetailDto?> AbrufenAsync(Guid id, CancellationToken ct);

    /// <summary>Vorheriger und nächster Beleg in der Liste, die <paramref name="filter"/> ergibt.</summary>
    Task<BelegNachbarnDto> NachbarnAsync(Guid id, BelegFilterDto filter, CancellationToken ct);

    Task<IReadOnlyList<BelegListeneintragDto>> AehnlicheAsync(Guid id, int anzahl, CancellationToken ct);
}
