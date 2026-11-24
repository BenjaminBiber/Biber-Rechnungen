using Microsoft.AspNetCore.Routing;

namespace Biber.Invoice.Domain.Module;

/// <summary>
/// Ein Modul, das HTTP-Endpunkte zuordnet. Wird beim Start per Assembly-Scan gefunden
/// (Assemblies Api und Infrastructure) und braucht einen parameterlosen Konstruktor.
/// </summary>
public interface IEndpunktModul
{
    void Zuordnen(IEndpointRouteBuilder app);
}
