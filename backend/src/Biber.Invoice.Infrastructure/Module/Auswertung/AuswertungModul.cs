using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Auswertung;

/// <summary>Dashboard, Kostenauswertung und gesammelter Systemstatus.</summary>
public sealed class AuswertungModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<IAuswertungsDienst, AuswertungsDienst>();
        dienste.AddScoped<SystemStatusSammler>();
    }
}
