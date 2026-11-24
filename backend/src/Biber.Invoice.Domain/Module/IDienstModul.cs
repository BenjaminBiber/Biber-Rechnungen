using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Domain.Module;

/// <summary>
/// Ein Modul, das Dienste registriert. Wird beim Start per Assembly-Scan gefunden
/// (Assemblies Api und Infrastructure) und braucht einen parameterlosen Konstruktor.
/// </summary>
public interface IDienstModul
{
    void Registrieren(IServiceCollection dienste, IConfiguration konfiguration);
}
