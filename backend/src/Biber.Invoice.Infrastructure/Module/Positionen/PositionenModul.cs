using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Positionen;

/// <summary>Registriert die Positionsverarbeitung (Pipeline-Schritt 5).</summary>
public sealed class PositionenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<IPipelineSchritt, PositionenSchritt>();
}
