using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Logos;

/// <summary>Registriert Logo-Dienst, Favicon-Abruf und den nächtlichen Favicon-Job.</summary>
public sealed class LogosModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddSingleton<IFaviconAbruf>(sp => new FaviconAbruf(sp.GetService<ILogger<FaviconAbruf>>()));
        dienste.AddScoped<ILogoDienst, LogoDienst>();
        dienste.AddScoped<IWiederkehrenderJob, FaviconJob>();
    }
}
