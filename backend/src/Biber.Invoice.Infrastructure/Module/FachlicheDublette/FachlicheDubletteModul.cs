using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.FachlicheDublette;

/// <summary>Registriert die Erkennung fachlicher Dubletten (Pipeline-Schritt 6).</summary>
public sealed class FachlicheDubletteModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<IPipelineSchritt, FachlicheDubletteSchritt>();
}
