using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.TagsKategorien;

/// <summary>Registriert die Pflege von Tags und Kategorien.</summary>
public sealed class TagsKategorienModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<TagVerwaltung>();
        dienste.AddScoped<KategorieVerwaltung>();
    }
}
