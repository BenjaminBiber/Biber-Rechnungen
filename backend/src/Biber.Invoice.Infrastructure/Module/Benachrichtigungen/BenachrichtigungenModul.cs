using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Benachrichtigungen;

/// <summary>Registriert <see cref="IBenachrichtigungsDienst"/> und die Abfragen der Glocke.</summary>
public sealed class BenachrichtigungenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<IBenachrichtigungsDienst, BenachrichtigungsDienst>();
        dienste.AddScoped<BenachrichtigungsAbfrage>();
    }
}
