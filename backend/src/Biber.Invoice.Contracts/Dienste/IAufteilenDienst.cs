using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Import;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Teilt einen mehrseitigen PDF-Beleg in neue Belege; das Original bleibt unverändert.</summary>
public interface IAufteilenDienst
{
    /// <returns>Je Teil der Import-Vorgang; <c>null</c>, wenn es den Beleg nicht gibt.</returns>
    Task<IReadOnlyList<ImportVorgangDto>?> AufteilenAsync(Guid belegId, AufteilenDto anfrage, CancellationToken ct);
}
