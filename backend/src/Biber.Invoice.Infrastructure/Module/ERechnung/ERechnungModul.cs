using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.ERechnung;

/// <summary>Pipeline-Schritt 3a: E-Rechnung (CII und UBL) lesen.</summary>
public sealed class ERechnungModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddSingleton<IERechnungParser, ERechnungParser>();
        dienste.AddScoped<IPipelineSchritt, ERechnungSchritt>();
    }
}
