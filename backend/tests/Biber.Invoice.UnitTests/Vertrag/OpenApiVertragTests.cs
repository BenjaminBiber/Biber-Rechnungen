using System.Text.Json;
using System.Text.Json.Nodes;

namespace Biber.Invoice.UnitTests.Vertrag;

/// <summary>
/// Hält <c>docs/api/openapi.json</c>, <c>ApiRouten</c> und die DTO-Records in <c>Contracts</c> deckungsgleich.
/// Nach einer Vertragsänderung die Datei neu erzeugen: <c>BIBER_OPENAPI_SCHREIBEN=1 dotnet test --filter OpenApiVertragTests</c>.
/// </summary>
public sealed class OpenApiVertragTests
{
    private static readonly Lazy<JsonObject> Dokument = new(() =>
        JsonNode.Parse(File.ReadAllText(VertragsTypen.OpenApiPfad()))!.AsObject());

    private static JsonObject Pfade => Dokument.Value["paths"]!.AsObject();

    private static JsonObject Schemas => Dokument.Value["components"]!["schemas"]!.AsObject();

    [Fact]
    public void Datei_entspricht_dem_erzeugten_Vertrag()
    {
        var erzeugt = OpenApiErzeuger.Erzeugen();
        var pfad = VertragsTypen.OpenApiPfad();
        if (Environment.GetEnvironmentVariable("BIBER_OPENAPI_SCHREIBEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
            File.WriteAllText(pfad, erzeugt);
        }

        File.ReadAllText(pfad).ReplaceLineEndings("\n").ShouldBe(
            erzeugt,
            "docs/api/openapi.json ist veraltet – mit BIBER_OPENAPI_SCHREIBEN=1 dotnet test --filter OpenApiVertragTests neu erzeugen");
    }

    [Fact]
    public void Jede_Route_aus_ApiRouten_steht_als_Pfad_in_der_Datei()
    {
        var fehlend = VertragsTypen.Routen()
            .Where(route => !Pfade.ContainsKey(route.Pfad))
            .Select(route => $"{route.Klasse}.{route.Name} = {route.Pfad}");

        fehlend.ShouldBeEmpty();
    }

    [Fact]
    public void Jeder_Pfad_der_Datei_ist_eine_Route_aus_ApiRouten()
    {
        var routen = VertragsTypen.Routen().Select(route => route.Pfad).ToHashSet();

        Pfade.Select(pfad => pfad.Key).Where(pfad => !routen.Contains(pfad)).ShouldBeEmpty();
    }

    [Fact]
    public void Jedes_Objekt_Schema_hat_einen_gleichnamigen_Record_und_umgekehrt()
    {
        var records = VertragsTypen.DtoRecords().Select(RecordName).ToHashSet();
        var schemas = ObjektSchemas().Select(schema => RecordName(schema.Key)).ToHashSet();

        schemas.Except(records).ShouldBeEmpty("Schemas ohne Record");
        records.Except(schemas).ShouldBeEmpty("Records ohne Schema");
    }

    [Fact]
    public void Eigenschaften_von_Schema_und_Record_stimmen_ueberein()
    {
        var records = VertragsTypen.DtoRecords().ToDictionary(RecordName);
        foreach (var (name, schema) in ObjektSchemas())
        {
            var record = records[RecordName(name)];
            var erwartet = VertragsTypen.Eigenschaften(record).Select(eigenschaft => JsonNamingPolicy.CamelCase.ConvertName(eigenschaft.Name));
            var tatsaechlich = schema!["properties"]!.AsObject().Select(eigenschaft => eigenschaft.Key);

            tatsaechlich.ShouldBe(erwartet, ignoreOrder: true, customMessage: name);
        }
    }

    [Fact]
    public void Jedes_Enum_Schema_hat_ein_gleichnamiges_Enum_mit_denselben_Namen()
    {
        var enums = VertragsTypen.Enums().ToDictionary(typ => typ.Name);
        foreach (var (name, schema) in Schemas.Where(schema => schema.Value!["enum"] is not null))
        {
            enums.ShouldContainKey(name);
            schema!["enum"]!.AsArray().Select(wert => wert!.GetValue<string>()).ShouldBe(Enum.GetNames(enums[name]), customMessage: name);
        }
    }

    [Fact]
    public void Jede_Operation_hat_eine_Richtlinie_oder_ist_ausdruecklich_anonym()
    {
        var richtlinien = new[] { "Lesen", "Bearbeiten", "Admin" };
        foreach (var (pfad, operationen) in Pfade)
        {
            foreach (var (methode, operation) in operationen!.AsObject())
            {
                var anonym = operation!["security"] is JsonArray { Count: 0 };
                var richtlinie = operation["x-richtlinie"]?.GetValue<string>();

                (anonym || richtlinien.Contains(richtlinie)).ShouldBeTrue($"{methode} {pfad}");
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> ObjektSchemas() =>
        Schemas.Where(schema => schema.Value!["type"]?.GetValue<string>() == "object");

    /// <summary>Record-Name zu einem Schema-Namen; <c>SeiteDtoOfImportVorgangDto</c> gehört zu <c>SeiteDto&lt;T&gt;</c>.</summary>
    private static string RecordName(string schemaName)
    {
        var generisch = VertragsTypen.DtoRecords()
            .Where(typ => typ.IsGenericTypeDefinition)
            .Select(RecordName)
            .FirstOrDefault(name => schemaName.StartsWith(name + "Of", StringComparison.Ordinal));
        return generisch ?? schemaName;
    }

    private static string RecordName(Type typ) => typ.IsGenericTypeDefinition ? typ.Name[..typ.Name.IndexOf('`')] : typ.Name;
}
