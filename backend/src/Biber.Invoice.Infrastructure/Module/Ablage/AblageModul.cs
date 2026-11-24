using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Ablage;

/// <summary>Registriert die WORM-Ablage der Originale (Pipeline-Schritt 9).</summary>
public sealed class AblageModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<IAblageSchreiber, AblageSchreiber>();
        dienste.AddScoped<IPipelineSchritt, AblageSchritt>();
    }
}
