using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen;

/// <summary>Registriert die Plausibilitätsprüfungen, den Prüfdienst und Pipeline-Schritt 8.</summary>
public sealed class PruefungenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddSingleton<IPruefung, PositionssummePruefung>();
        dienste.AddSingleton<IPruefung, NettoSteuerBruttoPruefung>();
        dienste.AddSingleton<IPruefung, PflichtfelderPruefung>();
        dienste.AddSingleton<IPruefung, DatumPruefung>();
        dienste.AddSingleton<IPruefung, Paragraph14Pruefung>();
        dienste.AddSingleton<IPruefung, DublettePruefung>();
        dienste.AddSingleton<IPruefung, NeuerKorrespondentPruefung>();
        dienste.AddSingleton<IPruefung, KonfidenzPruefung>();
        dienste.AddSingleton<IPruefung, KositPruefung>();
        dienste.AddSingleton<IPruefung, BankverbindungPruefung>();
        dienste.AddSingleton<IPruefung, UstIdNrPruefung>();
        dienste.AddScoped<IPruefDienst, PruefDienst>();
        dienste.AddScoped<IPipelineSchritt, PruefungenSchritt>();
    }
}
