using Biber.Invoice.Domain.Module;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Aufbewahrung;

/// <summary>Löschsperre bis Fristende und Löschfreigabe durch den Admin.</summary>
public sealed class AufbewahrungModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<IAufbewahrungsFreigabe, AufbewahrungsFreigabe>();
        dienste.AddScoped<IInterceptor, AufbewahrungsSchutz>();
        dienste.AddScoped<AufbewahrungsDienst>();
        dienste.AddScoped<GeloeschteBelegeDienst>();
    }
}
