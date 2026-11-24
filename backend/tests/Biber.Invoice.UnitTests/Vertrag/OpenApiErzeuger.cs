using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Biber.Invoice.Contracts;

namespace Biber.Invoice.UnitTests.Vertrag;

/// <summary>
/// Erzeugt <c>docs/api/openapi.json</c> (OpenAPI 3.1) aus <see cref="ApiEndpunkte"/> und den Records in
/// <c>Contracts/Dto</c>. JSON-Eigenschaften in camelCase, Enums als Text mit den C#-Namen.
/// </summary>
internal sealed class OpenApiErzeuger
{
    private static readonly JsonSerializerOptions Ausgabe = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] EinstellungsGruppen = ["allgemein", "ust", "pruefungen", "di", "mail", "ordner", "aufbewahrung", "zugriff"];

    private readonly SortedDictionary<string, JsonObject?> _schemas = new(StringComparer.Ordinal);
    private readonly NullabilityInfoContext _nullbarkeit = new();
    private readonly Dictionary<string, (string Klasse, string Name)> _routenNamen =
        VertragsTypen.Routen().ToDictionary(route => route.Pfad, route => (route.Klasse, route.Name));

    public static string Erzeugen() => new OpenApiErzeuger().Dokument().ToJsonString(Ausgabe).ReplaceLineEndings("\n") + "\n";

    private JsonObject Dokument()
    {
        foreach (var typ in VertragsTypen.DtoRecords().Where(typ => !typ.IsGenericTypeDefinition))
        {
            SchemaRef(typ);
        }

        var pfade = new JsonObject();
        foreach (var gruppe in ApiEndpunkte.Alle.GroupBy(endpunkt => endpunkt.Pfad))
        {
            var pfad = new JsonObject();
            foreach (var endpunkt in gruppe)
            {
                pfad[endpunkt.Methode] = Operation(endpunkt);
            }

            pfade[gruppe.Key] = pfad;
        }

        var schemas = new JsonObject();
        foreach (var (name, schema) in _schemas)
        {
            schemas[name] = schema;
        }

        var tags = new JsonArray();
        foreach (var klasse in ApiEndpunkte.Alle.Select(endpunkt => _routenNamen[endpunkt.Pfad].Klasse).Distinct())
        {
            tags.Add(new JsonObject { ["name"] = klasse });
        }

        return new JsonObject
        {
            ["openapi"] = "3.1.0",
            ["info"] = new JsonObject
            {
                ["title"] = "Biber-Rechnungen API",
                ["version"] = "1.0.0",
                ["description"] = "HTTP-Schnittstelle von Biber-Rechnungen. Erzeugt von OpenApiVertragTests "
                    + "(BIBER_OPENAPI_SCHREIBEN=1 dotnet test); nicht von Hand ändern. "
                    + "x-richtlinie nennt die Autorisierungsrichtlinie (Lesen, Bearbeiten, Admin).",
            },
            ["servers"] = new JsonArray(new JsonObject { ["url"] = "http://localhost:8080", ["description"] = "Lokale Entwicklung" }),
            ["security"] = new JsonArray(new JsonObject { ["cookie"] = new JsonArray() }, new JsonObject { ["apiToken"] = new JsonArray() }),
            ["tags"] = tags,
            ["paths"] = pfade,
            ["components"] = new JsonObject
            {
                ["securitySchemes"] = new JsonObject
                {
                    ["cookie"] = new JsonObject { ["type"] = "apiKey", ["in"] = "cookie", ["name"] = ".AspNetCore.Identity.Application" },
                    ["apiToken"] = new JsonObject { ["type"] = "http", ["scheme"] = "bearer", ["description"] = "Persönliches API-Token" },
                },
                ["responses"] = new JsonObject
                {
                    ["Ungueltig"] = Problem("Ungültige Anfrage; Validierungsfehler stehen in errors"),
                    ["NichtAngemeldet"] = Problem("Nicht angemeldet"),
                    ["KeinRecht"] = Problem("Fehlendes Recht"),
                    ["NichtGefunden"] = Problem("Nicht gefunden"),
                },
                ["schemas"] = schemas,
            },
        };
    }

    private JsonObject Operation(Endpunkt endpunkt)
    {
        var (klasse, name) = _routenNamen[endpunkt.Pfad];
        var operation = new JsonObject
        {
            ["tags"] = new JsonArray(klasse),
            ["summary"] = endpunkt.Zusammenfassung,
            ["operationId"] = endpunkt.Methode + klasse + name,
        };

        if (endpunkt.Richtlinie is null)
        {
            operation["security"] = new JsonArray();
        }
        else
        {
            operation["x-richtlinie"] = endpunkt.Richtlinie;
        }

        var parameter = Parameter(endpunkt);
        if (parameter.Count > 0)
        {
            operation["parameters"] = parameter;
        }

        var body = Body(endpunkt);
        if (body is not null)
        {
            operation["requestBody"] = body;
        }

        operation["responses"] = Antworten(endpunkt, body is not null || parameter.Count > 0);
        return operation;
    }

    private JsonArray Parameter(Endpunkt endpunkt)
    {
        var parameter = new JsonArray();
        foreach (var segment in endpunkt.Pfad.Split('/').Where(segment => segment.StartsWith('{')))
        {
            var name = segment.Trim('{', '}');
            var schema = name == "gruppe"
                ? new JsonObject { ["type"] = "string", ["enum"] = new JsonArray([.. EinstellungsGruppen.Select(gruppe => (JsonNode)gruppe)]) }
                : Einfach("string", "uuid");
            parameter.Add(new JsonObject { ["name"] = name, ["in"] = "path", ["required"] = true, ["schema"] = schema });
        }

        var abfragen = endpunkt.Query.AsEnumerable();
        if (endpunkt.QueryObjekt is not null)
        {
            var standard = Activator.CreateInstance(endpunkt.QueryObjekt);
            abfragen = VertragsTypen.Eigenschaften(endpunkt.QueryObjekt)
                .Select(eigenschaft => new Abfrage(JsonNamingPolicy.CamelCase.ConvertName(eigenschaft.Name), eigenschaft.PropertyType, eigenschaft.GetValue(standard)));
        }

        foreach (var abfrage in abfragen)
        {
            // Fehlt ein Query-Parameter, gilt er als null; nullbar wird er dadurch nicht.
            var schema = BasisSchema(Nullable.GetUnderlyingType(abfrage.Typ) ?? abfrage.Typ);
            if (abfrage.Werte is not null)
            {
                schema["enum"] = new JsonArray([.. abfrage.Werte.Select(wert => (JsonNode)wert)]);
            }

            var standard = Wert(abfrage.Standard);
            if (standard is not null && schema["$ref"] is null)
            {
                schema["default"] = standard;
            }

            var eintrag = new JsonObject { ["name"] = abfrage.Name, ["in"] = "query", ["required"] = abfrage.Pflicht, ["schema"] = schema };
            parameter.Add(eintrag);
        }

        return parameter;
    }

    private JsonObject? Body(Endpunkt endpunkt)
    {
        JsonNode? schema = null;
        var medientyp = "application/json";
        if (endpunkt.Body is not null)
        {
            schema = Schema(endpunkt.Body, nullbar: false);
        }
        else if (endpunkt.BodyVarianten.Length > 0)
        {
            schema = new JsonObject { ["oneOf"] = new JsonArray([.. endpunkt.BodyVarianten.Select(typ => Schema(typ, nullbar: false))]) };
        }
        else if (endpunkt.Multipart is not null)
        {
            medientyp = "multipart/form-data";
            var datei = Einfach("string", "binary");
            schema = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    [endpunkt.Multipart] = endpunkt.MultipartMehrere ? new JsonObject { ["type"] = "array", ["items"] = datei } : datei,
                },
                ["required"] = new JsonArray(endpunkt.Multipart),
            };
        }

        return schema is null
            ? null
            : new JsonObject { ["required"] = true, ["content"] = new JsonObject { [medientyp] = new JsonObject { ["schema"] = schema } } };
    }

    private JsonObject Antworten(Endpunkt endpunkt, bool kannUngueltigSein)
    {
        var erfolg = new JsonObject
        {
            ["description"] = endpunkt.Status switch
            {
                201 => "Angelegt",
                202 => "Angenommen",
                204 => "Erledigt",
                302 => "Weiterleitung",
                _ => "OK",
            },
        };

        if (endpunkt.Antwort is not null)
        {
            erfolg["content"] = new JsonObject { ["application/json"] = new JsonObject { ["schema"] = Schema(endpunkt.Antwort, nullbar: false) } };
        }
        else if (endpunkt.Datei is not null)
        {
            erfolg["content"] = new JsonObject { [endpunkt.Datei] = new JsonObject { ["schema"] = Einfach("string", "binary") } };
        }
        else if (endpunkt.Status == 302)
        {
            erfolg["headers"] = new JsonObject { ["Location"] = new JsonObject { ["schema"] = Einfach("string", "uri") } };
        }

        var antworten = new JsonObject { [endpunkt.Status.ToString(System.Globalization.CultureInfo.InvariantCulture)] = erfolg };
        if (kannUngueltigSein)
        {
            antworten["400"] = Verweis("#/components/responses/Ungueltig");
        }

        if (endpunkt.Richtlinie is not null)
        {
            antworten["401"] = Verweis("#/components/responses/NichtAngemeldet");
            antworten["403"] = Verweis("#/components/responses/KeinRecht");
        }

        if (endpunkt.Pfad.Contains('{'))
        {
            antworten["404"] = Verweis("#/components/responses/NichtGefunden");
        }

        return antworten;
    }

    private JsonNode Schema(Type typ, bool nullbar)
    {
        var basis = BasisSchema(Nullable.GetUnderlyingType(typ) ?? typ);
        nullbar |= Nullable.GetUnderlyingType(typ) is not null;
        if (!nullbar)
        {
            return basis;
        }

        if (basis["type"] is JsonValue einfach)
        {
            basis["type"] = new JsonArray(einfach.GetValue<string>(), "null");
            return basis;
        }

        return new JsonObject { ["anyOf"] = new JsonArray(basis, new JsonObject { ["type"] = "null" }) };
    }

    private JsonObject BasisSchema(Type typ)
    {
        if (typ == typeof(string)) return Einfach("string");
        if (typ == typeof(bool)) return Einfach("boolean");
        if (typ == typeof(int)) return Einfach("integer", "int32");
        if (typ == typeof(long)) return Einfach("integer", "int64");
        if (typ == typeof(decimal)) return Einfach("number", "decimal");
        if (typ == typeof(double)) return Einfach("number", "double");
        if (typ == typeof(Guid)) return Einfach("string", "uuid");
        if (typ == typeof(DateOnly)) return Einfach("string", "date");
        if (typ == typeof(DateTimeOffset)) return Einfach("string", "date-time");
        if (typ.IsArray) return Liste(typ.GetElementType()!);

        if (typ.IsGenericType)
        {
            var definition = typ.GetGenericTypeDefinition();
            var argumente = typ.GetGenericArguments();
            if (definition == typeof(IReadOnlyList<>) || definition == typeof(List<>)) return Liste(argumente[0]);
            if (definition == typeof(Dictionary<,>))
            {
                return new JsonObject
                {
                    ["type"] = "object",
                    ["propertyNames"] = BasisSchema(argumente[0]),
                    ["additionalProperties"] = BasisSchema(argumente[1]),
                };
            }
        }

        return SchemaRef(typ);
    }

    private JsonObject Liste(Type element) => new() { ["type"] = "array", ["items"] = BasisSchema(element) };

    private JsonObject SchemaRef(Type typ)
    {
        var name = VertragsTypen.SchemaName(typ);
        if (!_schemas.ContainsKey(name))
        {
            _schemas[name] = null;
            _schemas[name] = typ.IsEnum ? EnumSchema(typ) : ObjektSchema(typ);
        }

        return Verweis("#/components/schemas/" + name);
    }

    private static JsonObject EnumSchema(Type typ) =>
        new() { ["type"] = "string", ["enum"] = new JsonArray([.. Enum.GetNames(typ).Select(name => (JsonNode)name)]) };

    private JsonObject ObjektSchema(Type typ)
    {
        if (!VertragsTypen.IstRecord(typ))
        {
            throw new InvalidOperationException($"{typ.Name} ist kein Record");
        }

        var standard = typ.GetConstructor(Type.EmptyTypes) is not null ? Activator.CreateInstance(typ) : null;
        var eigenschaften = new JsonObject();
        var pflicht = new JsonArray();
        foreach (var eigenschaft in VertragsTypen.Eigenschaften(typ))
        {
            var jsonName = JsonNamingPolicy.CamelCase.ConvertName(eigenschaft.Name);
            var nullbar = _nullbarkeit.Create(eigenschaft).ReadState == NullabilityState.Nullable;
            var schema = Schema(eigenschaft.PropertyType, nullbar);
            var wert = standard is null ? null : Wert(eigenschaft.GetValue(standard));
            if (wert is not null && schema is JsonObject objekt && objekt["$ref"] is null)
            {
                objekt["default"] = wert;
            }

            eigenschaften[jsonName] = schema;
            if (!nullbar)
            {
                pflicht.Add(jsonName);
            }
        }

        var ergebnis = new JsonObject { ["type"] = "object", ["properties"] = eigenschaften };
        if (pflicht.Count > 0)
        {
            ergebnis["required"] = pflicht;
        }

        return ergebnis;
    }

    /// <summary>Standardwert als JSON; nur für einfache Werte, sonst <c>null</c>.</summary>
    private static JsonNode? Wert(object? wert) => wert switch
    {
        string text => text,
        bool wahr => wahr,
        int zahl => zahl,
        decimal betrag => betrag,
        Enum element => element.ToString(),
        _ => null,
    };

    private static JsonObject Einfach(string typ, string? format = null)
    {
        var schema = new JsonObject { ["type"] = typ };
        if (format is not null)
        {
            schema["format"] = format;
        }

        return schema;
    }

    private static JsonObject Verweis(string ziel) => new() { ["$ref"] = ziel };

    private static JsonObject Problem(string beschreibung) => new()
    {
        ["description"] = beschreibung,
        ["content"] = new JsonObject
        {
            ["application/problem+json"] = new JsonObject
            {
                ["schema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["description"] = "ProblemDetails nach RFC 9457",
                    ["properties"] = new JsonObject
                    {
                        ["type"] = Einfach("string"),
                        ["title"] = Einfach("string"),
                        ["status"] = Einfach("integer", "int32"),
                        ["detail"] = Einfach("string"),
                        ["errors"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["additionalProperties"] = new JsonObject { ["type"] = "array", ["items"] = Einfach("string") },
                        },
                    },
                },
            },
        },
    };
}
