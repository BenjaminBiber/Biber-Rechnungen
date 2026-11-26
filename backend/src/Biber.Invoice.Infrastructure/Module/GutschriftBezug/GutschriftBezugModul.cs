using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.GutschriftBezug;

/// <summary>Registriert die Zuordnung von Gutschriften zu Rechnungen (Pipeline-Schritt 6b).</summary>
public sealed class GutschriftBezugModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<IPipelineSchritt, GutschriftBezugSchritt>();
}
