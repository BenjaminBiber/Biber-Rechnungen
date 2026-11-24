using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.HashDublette;

/// <summary>Registriert den Dubletten-Check über den Datei-Hash (Pipeline-Schritt 1).</summary>
public sealed class HashDubletteModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<IPipelineSchritt, HashDubletteSchritt>();
}
