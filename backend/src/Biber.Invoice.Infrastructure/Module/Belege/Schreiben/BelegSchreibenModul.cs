using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Belege.Schreiben;

public sealed class BelegSchreibenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<IBelegBearbeitungsDienst, BelegBearbeitungsDienst>();
}
