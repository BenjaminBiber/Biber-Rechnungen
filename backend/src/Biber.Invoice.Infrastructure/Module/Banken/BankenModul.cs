using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Banken;

/// <summary>Registriert den Bundesbank-Client (<c>Bundesbank:BasisUrl</c>), den Dienst und den wiederkehrenden Abruf.</summary>
public sealed class BankenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddHttpClient<IBundesbankClient, BundesbankClient>(http =>
        {
            var basis = konfiguration["Bundesbank:BasisUrl"] ?? BundesbankClient.StandardBasisUrl;
            http.BaseAddress = new Uri(basis.EndsWith('/') ? basis : basis + "/");
            http.Timeout = TimeSpan.FromSeconds(60);
        });
        dienste.AddScoped<BankleitzahlenDienst>();
        dienste.AddScoped<IWiederkehrenderJob, BankleitzahlenJob>();
    }
}
