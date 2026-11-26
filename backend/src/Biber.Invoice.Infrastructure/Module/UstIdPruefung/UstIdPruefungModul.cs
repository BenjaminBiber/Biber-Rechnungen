using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.UstIdPruefung;

/// <summary>Registriert den VIES-Client (<c>Vies:BasisUrl</c>), den Prüfdienst und den wiederkehrenden Job.</summary>
public sealed class UstIdPruefungModul : IDienstModul
{
    public const string StandardBasisUrl = "https://ec.europa.eu/taxation_customs/vies/rest-api/";

    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddHttpClient<IViesClient, ViesClient>(http =>
        {
            var basis = konfiguration["Vies:BasisUrl"] ?? StandardBasisUrl;
            http.BaseAddress = new Uri(basis.EndsWith('/') ? basis : basis + "/");
            http.Timeout = TimeSpan.FromSeconds(20);
        });
        dienste.AddScoped<UstIdPruefDienst>();
        dienste.AddScoped<IUstIdPruefDienst>(sp => sp.GetRequiredService<UstIdPruefDienst>());
        dienste.AddScoped<IWiederkehrenderJob, UstIdPruefJob>();
    }
}
