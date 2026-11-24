using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Integritaet;

/// <summary>Nächtliche Integritätsprüfung der Ablage und Stand des Datenbank-Backups.</summary>
public sealed class IntegritaetModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<IntegritaetsPruefer>();
        dienste.AddScoped<IntegritaetsJob>();
        dienste.AddScoped<IWiederkehrenderJob>(sp => sp.GetRequiredService<IntegritaetsJob>());
        dienste.AddSingleton<IIntegritaetsStarter, HangfireIntegritaetsStarter>();
        dienste.AddSingleton<BackupStatusLeser>();
        dienste.AddScoped<ISystemStatusQuelle, IntegritaetsStatus>();
        dienste.AddSingleton<ISystemStatusQuelle, BackupStatus>();
    }
}
