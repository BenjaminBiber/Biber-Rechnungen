using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Korrespondenten;

/// <summary>Registriert <see cref="IKorrespondentDienst"/>.</summary>
public sealed class KorrespondentenModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration) =>
        dienste.AddScoped<IKorrespondentDienst, KorrespondentDienst>();
}
