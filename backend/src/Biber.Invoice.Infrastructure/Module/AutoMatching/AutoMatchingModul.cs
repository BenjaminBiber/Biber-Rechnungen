using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.AutoMatching;

/// <summary>Registriert Training (nächtlicher Job), Vorhersage, den Pipeline-Schritt Auto-Zuordnung und den Status.</summary>
public sealed class AutoMatchingModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<AutoMatchingTrainer>();
        dienste.AddScoped<IWiederkehrenderJob, AutoMatchingJob>();
        dienste.AddSingleton<AutoMatchingVorhersage>();
        dienste.AddScoped<IPipelineSchritt, AutoZuordnungSchritt>();
        dienste.AddScoped<IAutoMatchingDienst, AutoMatchingDienst>();
    }
}
