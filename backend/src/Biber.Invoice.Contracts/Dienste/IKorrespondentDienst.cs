using Biber.Invoice.Contracts.Dto.Korrespondenten;

namespace Biber.Invoice.Contracts.Dienste;

public interface IKorrespondentDienst
{
    /// <param name="typ">„Lieferanten“, „Kunden“ oder „Alle“.</param>
    /// <param name="jahr">Jahr für <see cref="KorrespondentListeneintragDto.KostenJahr"/>.</param>
    Task<IReadOnlyList<KorrespondentListeneintragDto>> ListeAsync(string? suche, string typ, int jahr, CancellationToken ct);

    /// <returns><c>null</c>, wenn es den Korrespondenten nicht gibt.</returns>
    Task<KorrespondentDetailDto?> AbrufenAsync(Guid id, int jahr, CancellationToken ct);

    Task<KorrespondentDetailDto> AnlegenAsync(KorrespondentAendernDto korrespondent, CancellationToken ct);

    /// <returns><c>null</c>, wenn es den Korrespondenten nicht gibt.</returns>
    Task<KorrespondentDetailDto?> AendernAsync(Guid id, KorrespondentAendernDto korrespondent, CancellationToken ct);

    /// <summary>Hängt Belege, Artikel und Aliase der Quellen an <paramref name="behaltenId"/> und löscht die Quellen.</summary>
    /// <returns><c>null</c>, wenn es <paramref name="behaltenId"/> nicht gibt.</returns>
    Task<KorrespondentDetailDto?> ZusammenfuehrenAsync(Guid behaltenId, IReadOnlyList<Guid> quellIds, CancellationToken ct);
}
