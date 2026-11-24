using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Kosit;

/// <summary>Registriert den KoSIT-Client und den Pipeline-Schritt „KoSIT-Validierung“.</summary>
public sealed class KositModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddHttpClient<IKositClient, KositClient>(http =>
        {
            var basis = konfiguration["Kosit:BasisUrl"] ?? "http://kosit:8080";
            http.BaseAddress = new Uri(basis.EndsWith('/') ? basis : basis + "/");
            http.Timeout = TimeSpan.FromSeconds(60);
        });
        dienste.AddScoped<IPipelineSchritt, KositSchritt>();
    }
}
