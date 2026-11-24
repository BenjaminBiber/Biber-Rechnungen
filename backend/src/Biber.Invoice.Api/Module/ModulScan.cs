using System.Reflection;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Persistenz;

namespace Biber.Invoice.Api.Module;

/// <summary>
/// Findet alle Module (<see cref="IDienstModul"/>, <see cref="IEndpunktModul"/>) in den Assemblies
/// Api und Infrastructure.
/// </summary>
public static class ModulScan
{
    public static IReadOnlyList<Assembly> Assemblies { get; } =
    [
        typeof(Program).Assembly,
        typeof(BiberDbContext).Assembly,
    ];

    public static IReadOnlyList<Type> DienstModule { get; } = Finde<IDienstModul>();

    public static IReadOnlyList<Type> EndpunktModule { get; } = Finde<IEndpunktModul>();

    /// <summary>
    /// Ruft <see cref="IDienstModul.Registrieren"/> aller Dienstmodule auf und registriert alle
    /// Endpunktmodule als Singleton <see cref="IEndpunktModul"/>. Tests können so weitere
    /// Endpunktmodule über den DI-Container beisteuern.
    /// </summary>
    public static void RegistriereModule(this IServiceCollection dienste, IConfiguration konfiguration)
    {
        foreach (var typ in DienstModule)
        {
            var modul = (IDienstModul)Activator.CreateInstance(typ)!;
            modul.Registrieren(dienste, konfiguration);
        }

        foreach (var typ in EndpunktModule)
        {
            dienste.AddSingleton(typeof(IEndpunktModul), typ);
        }
    }

    /// <summary>Ordnet die Endpunkte aller im DI-Container registrierten Endpunktmodule zu.</summary>
    public static void OrdneModuleZu(this WebApplication app)
    {
        foreach (var modul in app.Services.GetServices<IEndpunktModul>())
        {
            modul.Zuordnen(app);
        }
    }

    private static Type[] Finde<T>() =>
        Assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(T).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();
}
