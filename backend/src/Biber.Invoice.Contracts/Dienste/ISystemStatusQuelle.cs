using Biber.Invoice.Contracts.Dto.Auswertung;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>
/// Liefert einen Eintrag für <c>GET system/status</c>. Jedes Modul registriert seine eigene Quelle;
/// der Status-Endpunkt sammelt alle registrierten Quellen ein.
/// </summary>
public interface ISystemStatusQuelle
{
    string Schluessel { get; }

    Task<SystemStatusEintragDto> LesenAsync(CancellationToken ct);
}
