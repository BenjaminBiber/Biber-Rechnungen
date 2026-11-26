using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Belege;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Ändernder Zugriff auf Belege. Methoden mit <c>bool</c> liefern <c>false</c>, wenn es den Beleg nicht gibt.</summary>
public interface IBelegBearbeitungsDienst
{
    /// <returns>Der geänderte Beleg; <c>null</c>, wenn es ihn nicht gibt.</returns>
    Task<BelegDetailDto?> AendernAsync(Guid id, BelegAendernDto aenderung, CancellationToken ct);

    /// <returns>Der geänderte Beleg; <c>null</c>, wenn es ihn nicht gibt.</returns>
    Task<BelegDetailDto?> PositionenErsetzenAsync(Guid id, IReadOnlyList<PositionAendernDto> positionen, CancellationToken ct);

    Task<bool> TagHinzufuegenAsync(Guid id, Guid tagId, CancellationToken ct);

    Task<bool> TagEntfernenAsync(Guid id, Guid tagId, CancellationToken ct);

    /// <summary>Entfernt den System-Tag „zu prüfen“.</summary>
    Task<bool> BestaetigenAsync(Guid id, string? begruendung, CancellationToken ct);

    Task<AnzahlGeaendertDto> MassenaktionAsync(MassenaktionDto aktion, CancellationToken ct);

    /// <param name="duplikatVonId"><c>null</c> hebt die Dubletten-Markierung auf.</param>
    Task<bool> DubletteSetzenAsync(Guid id, Guid? duplikatVonId, CancellationToken ct);

    /// <summary>Verknüpft eine Gutschrift mit einer Rechnung desselben Korrespondenten.</summary>
    /// <param name="bezugBelegId"><c>null</c> löst die Verknüpfung.</param>
    Task<bool> BezugSetzenAsync(Guid id, Guid? bezugBelegId, CancellationToken ct);

    /// <summary>Setzt oder entfernt das Skonto einer Rechnung.</summary>
    /// <returns>Der geänderte Beleg; <c>null</c>, wenn es ihn nicht gibt.</returns>
    Task<BelegDetailDto?> SkontoSetzenAsync(Guid id, SkontoSetzenDto skonto, CancellationToken ct);

    /// <summary>Stößt die Extraktion für den Beleg erneut an.</summary>
    Task<bool> ErneutAuslesenAsync(Guid id, CancellationToken ct);
}
