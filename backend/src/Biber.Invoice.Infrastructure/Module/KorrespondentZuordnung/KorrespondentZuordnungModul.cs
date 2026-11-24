using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.KorrespondentZuordnung;

/// <summary>Registriert die Korrespondenten-Zuordnung (Pipeline-Schritt 4).</summary>
public sealed class KorrespondentZuordnungModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<IPipelineSchritt, KorrespondentZuordnungSchritt>();
}
