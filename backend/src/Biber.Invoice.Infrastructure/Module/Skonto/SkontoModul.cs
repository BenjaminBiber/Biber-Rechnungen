using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Skonto;

/// <summary>Registriert den Hinweis auf ablaufende Skontofristen.</summary>
public sealed class SkontoModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<IWiederkehrenderJob, SkontoHinweisJob>();
}
