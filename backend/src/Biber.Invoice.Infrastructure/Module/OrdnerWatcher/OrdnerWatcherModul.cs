using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.OrdnerWatcher;

/// <summary>Ordner-Watcher für den Eingangsordner; der Hintergrunddienst läuft nur bei <c>OrdnerWatcher:Aktiv</c> (Standard <c>true</c>).</summary>
public sealed class OrdnerWatcherModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddSingleton<OrdnerAbtaster>();
        dienste.AddSingleton<ISystemStatusQuelle, EingangsordnerStatus>();
        if (konfiguration.GetValue("OrdnerWatcher:Aktiv", true))
        {
            dienste.AddHostedService<OrdnerWatcherDienst>();
        }
    }
}
