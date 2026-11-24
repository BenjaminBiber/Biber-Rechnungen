using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Suche;

public sealed class SuchModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<ISuchDienst, SuchDienst>();
    }
}
