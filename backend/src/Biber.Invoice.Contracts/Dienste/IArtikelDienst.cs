using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Artikel;

namespace Biber.Invoice.Contracts.Dienste;

public interface IArtikelDienst
{
    Task<SeiteDto<ArtikelListeneintragDto>> SuchenAsync(ArtikelFilterDto filter, CancellationToken ct);

    /// <returns><c>null</c>, wenn es den Artikel nicht gibt.</returns>
    Task<PreisDetailDto?> ArtikelDetailAsync(Guid id, int jahr, CancellationToken ct);

    Task<IReadOnlyList<StammartikelListeneintragDto>> StammartikelListeAsync(CancellationToken ct);

    /// <returns><c>null</c>, wenn es den Stammartikel nicht gibt.</returns>
    Task<PreisDetailDto?> StammartikelDetailAsync(Guid id, int jahr, CancellationToken ct);

    /// <summary>Legt einen Stammartikel an (<paramref name="id"/> = <c>null</c>) oder ändert ihn samt Zuordnungen.</summary>
    /// <returns><c>null</c>, wenn es den Stammartikel <paramref name="id"/> nicht gibt.</returns>
    Task<StammartikelListeneintragDto?> StammartikelSpeichernAsync(Guid? id, StammartikelAendernDto stammartikel, CancellationToken ct);

    /// <summary>Löst einen Artikel aus dem Stammartikel.</summary>
    /// <returns><c>false</c>, wenn die Zuordnung nicht existiert.</returns>
    Task<bool> ArtikelHerausloesenAsync(Guid stammartikelId, Guid artikelId, CancellationToken ct);
}
