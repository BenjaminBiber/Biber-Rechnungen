using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Formaterkennung;

public sealed class FormaterkennungModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddSingleton<IFormatErkenner, FormatErkenner>();
        dienste.AddScoped<IPipelineSchritt, FormaterkennungSchritt>();
    }
}
