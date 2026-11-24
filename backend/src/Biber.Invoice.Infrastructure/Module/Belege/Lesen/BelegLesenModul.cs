using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Belege.Lesen;

public sealed class BelegLesenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<IBelegAbfrageDienst, BelegAbfrageDienst>();
        dienste.AddScoped<BelegDateiQuelle>();
        dienste.AddScoped<AnsichtenDienst>();
    }
}
