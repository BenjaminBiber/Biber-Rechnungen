using System.Threading.RateLimiting;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Module;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Registrierung;

/// <summary>
/// Offene Registrierung mit Freigabe durch den Admin. Meldet <c>RegistrierungOffen</c> im Auth-Status und
/// begrenzt <c>POST /api/auth/registrieren</c> auf <see cref="MaxRegistrierungenJeStunde"/> je IP-Adresse und Stunde.
/// </summary>
public sealed class RegistrierungModul : IDienstModul
{
    /// <summary>Name der Begrenzungsrichtlinie; nur der Registrieren-Endpunkt nutzt sie.</summary>
    public const string BegrenzungsRichtlinie = "registrierung";

    public const int MaxRegistrierungenJeStunde = 5;

    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<RegistrierungsDienst>();
        dienste.AddScoped<IAuthStatusErgaenzung, RegistrierungStatusErgaenzung>();
        dienste.AddRateLimiter(optionen =>
        {
            optionen.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            optionen.AddPolicy(BegrenzungsRichtlinie, kontext => RateLimitPartition.GetFixedWindowLimiter(
                kontext.Connection.RemoteIpAddress?.ToString() ?? "unbekannt",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = MaxRegistrierungenJeStunde,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }));
        });
    }
}

internal sealed class RegistrierungStatusErgaenzung(IEinstellungen einstellungen) : IAuthStatusErgaenzung
{
    public async Task ErgaenzenAsync(AuthStatusErgaenzung status, CancellationToken ct) =>
        status.RegistrierungOffen = (await einstellungen.LesenAsync<ZugriffEinstellungen>(ct)).OffeneRegistrierung;
}
