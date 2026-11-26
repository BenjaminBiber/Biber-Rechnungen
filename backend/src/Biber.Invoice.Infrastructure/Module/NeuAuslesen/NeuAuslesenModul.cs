using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.NeuAuslesen;

/// <summary>„Alle Belege erneut auslesen“: Lauf mit Fortschritt, im Hintergrund über Hangfire.</summary>
public sealed class NeuAuslesenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<NeuAuslesenDienst>();
        dienste.AddScoped<NeuAuslesenJob>();
        dienste.AddSingleton<INeuAuslesenStarter, HangfireNeuAuslesenStarter>();
    }
}
