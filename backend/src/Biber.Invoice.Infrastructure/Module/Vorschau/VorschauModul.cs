using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Vorschau;

/// <summary>Registriert PDF-Renderer, Vorschau-Dienst und den Pipeline-Schritt „Vorschau“.</summary>
public sealed class VorschauModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddSingleton<IPdfRenderer, PdfRenderer>();
        dienste.AddScoped<IVorschauDienst, VorschauDienst>();
        dienste.AddScoped<IPipelineSchritt, VorschauSchritt>();
    }
}
