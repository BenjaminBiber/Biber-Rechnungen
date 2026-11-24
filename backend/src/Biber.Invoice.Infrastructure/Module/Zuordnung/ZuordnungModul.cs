using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Zuordnung;

/// <summary>Registriert die Zuordnung von Kategorie und Tags (Pipeline-Schritt 7) samt Regel-Test.</summary>
public sealed class ZuordnungModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<IZuordnungsDienst, ZuordnungsDienst>();
        dienste.AddScoped<IPipelineSchritt, ZuordnungSchritt>();
    }
}
