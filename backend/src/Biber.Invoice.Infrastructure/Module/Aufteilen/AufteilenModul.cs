using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Aufteilen;

/// <summary>Registriert das Aufteilen eines PDF-Belegs nach Seiten.</summary>
public sealed class AufteilenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddSingleton<IPdfTeiler, PdfTeiler>();
        dienste.AddScoped<IAufteilenDienst, AufteilenDienst>();
    }
}
