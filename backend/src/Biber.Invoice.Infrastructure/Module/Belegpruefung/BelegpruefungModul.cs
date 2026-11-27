using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Belegpruefung;

/// <summary>Sortiert Dateien aus, die kein Beleg sind (AGB, Lieferschein, Werbung …).</summary>
public sealed class BelegpruefungModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<IPipelineSchritt, BelegpruefungSchritt>();
        dienste.AddScoped<IPipelineSchritt, BelegpruefungNachAuslesenSchritt>();
    }
}
