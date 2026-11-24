using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Export;

/// <summary>Export für den Steuerberater: Belegliste, Summen je Kategorie, Vorsteuer und ZIP der Originale.</summary>
public sealed class ExportModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<ExportDienst>();
}
