using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Verfahrensdoku;

public sealed class VerfahrensdokuModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<VerfahrensdokuDienst>();
    }
}
