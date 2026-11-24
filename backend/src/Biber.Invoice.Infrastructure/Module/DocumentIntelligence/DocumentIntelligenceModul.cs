using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.DocumentIntelligence;

/// <summary>Pipeline-Schritt 3b (Document Intelligence), Verbrauchszähler und Monatswechsel-Job.</summary>
public sealed class DocumentIntelligenceModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.Configure<DocumentIntelligenceOptionen>(konfiguration.GetSection(DocumentIntelligenceOptionen.Abschnitt));
        dienste.AddSingleton<IDocumentIntelligenceClient, DocumentIntelligenceClient>();
        dienste.AddScoped<DiVerbrauchsZaehler>();
        dienste.AddScoped<IPipelineSchritt, DocumentIntelligenceSchritt>();
        dienste.AddScoped<IWiederkehrenderJob, DiMonatswechselJob>();
    }
}
