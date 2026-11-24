using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Einstellungen;

public sealed class EinstellungenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddMemoryCache();
        dienste.AddScoped<IEinstellungen, EinstellungenDienst>();
    }
}
