namespace Biber.Invoice.Domain.Module;

/// <summary>
/// Erweiterungspunkt für <c>GET /api/auth/status</c>: Module (Registrierung, Google-Anmeldung) setzen
/// ihre Werte in <see cref="AuthStatusErgaenzung"/>. Der Status-Endpunkt ruft alle registrierten
/// Ergänzungen auf; ohne Ergänzung bleiben die Werte <c>false</c>.
/// </summary>
public interface IAuthStatusErgaenzung
{
    Task ErgaenzenAsync(AuthStatusErgaenzung status, CancellationToken ct);
}

/// <summary>Veränderbare Teile des Anmeldestatus.</summary>
public sealed class AuthStatusErgaenzung
{
    public bool RegistrierungOffen { get; set; }

    public bool GoogleAktiv { get; set; }
}
