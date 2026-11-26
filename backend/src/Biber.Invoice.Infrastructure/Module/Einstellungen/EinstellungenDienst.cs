using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Biber.Invoice.Infrastructure.Module.Einstellungen;

/// <summary>
/// Speichert je Einstellungsgruppe einen Eintrag in <c>einstellung</c> (Wert als <c>jsonb</c>).
/// Schlüssel = Typname ohne Endung „Einstellungen“ in Kleinbuchstaben, z. B. <c>pruef</c>.
/// Gelesene Werte liegen im <see cref="IMemoryCache"/>; Speichern verwirft sie und schreibt
/// je geändertem Feld einen <see cref="AuditEintrag"/>.
/// </summary>
public sealed class EinstellungenDienst(
    BiberDbContext db,
    IMemoryCache cache,
    TimeProvider zeit,
    IAktuellerBenutzer benutzer) : IEinstellungen
{
    public const string AuditEntitaet = "Einstellung";

    /// <summary>Serialisierung der Werte: camelCase, Enums als Namen.</summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Schluessel<T>() => Schluessel(typeof(T));

    public static string Schluessel(Type typ)
    {
        const string endung = "Einstellungen";
        var name = typ.Name.EndsWith(endung, StringComparison.Ordinal) ? typ.Name[..^endung.Length] : typ.Name;
        return name.ToLowerInvariant();
    }

    public async Task<T> LesenAsync<T>(CancellationToken ct) where T : class, new()
    {
        var schluessel = Schluessel<T>();
        if (cache.TryGetValue(CacheSchluessel(schluessel), out T? zwischengespeichert) && zwischengespeichert is not null)
        {
            return zwischengespeichert;
        }

        var eintrag = await db.Einstellungen.AsNoTracking().SingleOrDefaultAsync(e => e.Schluessel == schluessel, ct);
        var wert = eintrag is null ? new T() : JsonSerializer.Deserialize<T>(eintrag.Wert, Json) ?? new T();
        cache.Set(CacheSchluessel(schluessel), wert);
        return wert;
    }

    public async Task SpeichernAsync<T>(T wert, CancellationToken ct) where T : class
    {
        ArgumentNullException.ThrowIfNull(wert);
        var schluessel = Schluessel(wert.GetType());
        var neu = JsonSerializer.SerializeToNode(wert, wert.GetType(), Json)!.AsObject();
        var jetzt = zeit.GetUtcNow();

        var eintrag = await db.Einstellungen.SingleOrDefaultAsync(e => e.Schluessel == schluessel, ct);
        JsonObject alt;
        if (eintrag is null)
        {
            var standard = Activator.CreateInstance(wert.GetType());
            alt = standard is null
                ? []
                : JsonSerializer.SerializeToNode(standard, wert.GetType(), Json)!.AsObject();
            eintrag = new Einstellung { Schluessel = schluessel };
            db.Einstellungen.Add(eintrag);
        }
        else
        {
            alt = JsonNode.Parse(eintrag.Wert)?.AsObject() ?? [];
        }

        eintrag.Wert = neu.ToJsonString(Json);
        eintrag.GeaendertAm = jetzt;
        var vertraulich = VertraulicheFelder(wert.GetType());

        foreach (var (feld, neuerWert) in neu)
        {
            alt.TryGetPropertyValue(feld, out var alterWert);
            if (JsonNode.DeepEquals(alterWert, neuerWert))
            {
                continue;
            }

            var geheim = vertraulich.Contains(feld);
            var vorher = geheim ? Maskiert(alterWert) : alterWert;
            var nachher = geheim ? Maskiert(neuerWert) : neuerWert;

            db.AuditEintraege.Add(new AuditEintrag
            {
                Zeitpunkt = jetzt,
                BenutzerId = benutzer.Id,
                BenutzerName = benutzer.IstSystem || benutzer.Id is null ? "System" : benutzer.Anzeigename,
                Entitaet = AuditEntitaet,
                EntitaetId = schluessel,
                Aktion = "Geaendert",
                Feld = feld,
                AlterWert = vorher?.ToJsonString(Json) ?? "null",
                NeuerWert = nachher?.ToJsonString(Json) ?? "null",
            });
        }

        await db.SaveChangesAsync(ct);
        cache.Remove(CacheSchluessel(schluessel));
    }

    private static string CacheSchluessel(string schluessel) => $"einstellungen:{schluessel}";

    /// <summary>JSON-Namen der mit <see cref="VertraulichAttribute"/> markierten Eigenschaften.</summary>
    private static HashSet<string> VertraulicheFelder(Type typ) =>
        typ.GetProperties()
            .Where(p => p.IsDefined(typeof(VertraulichAttribute), inherit: true))
            .Select(p => Json.PropertyNamingPolicy?.ConvertName(p.Name) ?? p.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static JsonNode? Maskiert(JsonNode? wert) => wert is null ? null : JsonValue.Create("***");
}

/// <summary>Der Wert dieser Einstellung erscheint im Audit-Protokoll nur als <c>***</c>.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class VertraulichAttribute : Attribute;
