using System.Reflection;
using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.UnitTests.Vertrag;

/// <summary>Reflexion über den Vertrag: Routen-Konstanten, DTO-Records und Enums.</summary>
internal static class VertragsTypen
{
    private const string DtoNamespace = "Biber.Invoice.Contracts.Dto";

    /// <summary>Alle Konstanten aus <see cref="ApiRouten"/> als (Klasse, Name, Pfad).</summary>
    public static IReadOnlyList<(string Klasse, string Name, string Pfad)> Routen() =>
        typeof(ApiRouten).GetNestedTypes()
            .SelectMany(klasse => klasse.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(feld => feld.IsLiteral && feld.FieldType == typeof(string))
                .Select(feld => (klasse.Name, feld.Name, (string)feld.GetRawConstantValue()!)))
            .ToList();

    /// <summary>Alle Records unter <c>Contracts/Dto</c>; generische als Typdefinition.</summary>
    public static IReadOnlyList<Type> DtoRecords() =>
        typeof(ApiRouten).Assembly.GetTypes()
            .Where(typ => typ.IsPublic && typ.Namespace?.StartsWith(DtoNamespace, StringComparison.Ordinal) == true)
            .Where(IstRecord)
            .OrderBy(typ => typ.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>Alle Enums aus Domain und Contracts.</summary>
    public static IReadOnlyList<Type> Enums() =>
        typeof(ApiRouten).Assembly.GetTypes().Concat(typeof(Rolle).Assembly.GetTypes())
            .Where(typ => typ.IsPublic && typ.IsEnum)
            .ToList();

    public static bool IstRecord(Type typ) =>
        typ.IsClass && typ.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance) is not null;

    /// <summary>Öffentliche Eigenschaften in Deklarationsreihenfolge.</summary>
    public static IEnumerable<PropertyInfo> Eigenschaften(Type typ) =>
        typ.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(eigenschaft => eigenschaft.MetadataToken);

    /// <summary>Schema-Name: Typname, bei Generika <c>SeiteDtoOfImportVorgangDto</c>.</summary>
    public static string SchemaName(Type typ) =>
        typ.IsGenericType
            ? typ.Name[..typ.Name.IndexOf('`')] + "Of" + string.Concat(typ.GetGenericArguments().Select(SchemaName))
            : typ.Name;

    /// <summary>Repo-Hauptordner (enthält <c>backend</c> und <c>docs</c>).</summary>
    public static string RepoOrdner()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !(Directory.Exists(Path.Combine(ordner.FullName, "backend")) && Directory.Exists(Path.Combine(ordner.FullName, "docs"))))
        {
            ordner = ordner.Parent;
        }

        return ordner?.FullName ?? throw new InvalidOperationException("Repo-Hauptordner nicht gefunden");
    }

    public static string OpenApiPfad() => Path.Combine(RepoOrdner(), "docs", "api", "openapi.json");
}
