using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol;

namespace Biber.Invoice.Api.Module.Mcp;

/// <summary>Gemeinsames der MCP-Tools: kompaktes JSON, Links in die Anwendung und Auflösen von Namen.</summary>
internal static class McpHilfe
{
    private static readonly JsonSerializerOptions JsonOptionen = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Json(object wert) => JsonSerializer.Serialize(wert, JsonOptionen);

    /// <summary>Link auf die Detailansicht; ohne <c>Anwendung:OeffentlicheUrl</c> relativ.</summary>
    public static string BelegLink(IConfiguration konfiguration, Guid id) =>
        $"{(konfiguration["Anwendung:OeffentlicheUrl"] ?? "").TrimEnd('/')}/belege/{id}";

    /// <summary>
    /// Sucht <paramref name="name"/> unter <paramref name="kandidaten"/>: erst exakt, dann als eindeutiger Teil des Namens.
    /// Ohne Treffer ein Tool-Fehler mit den ähnlichsten Namen.
    /// </summary>
    public static Guid Aufloesen(string art, string name, IEnumerable<(Guid Id, string Name)> kandidaten)
    {
        var liste = kandidaten.ToList();
        string gesucht = name.Trim();
        var exakt = liste.Where(k => string.Equals(k.Name, gesucht, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exakt.Count == 1)
        {
            return exakt[0].Id;
        }

        var enthalten = liste.Where(k => k.Name.Contains(gesucht, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exakt.Count == 0 && enthalten.Count == 1)
        {
            return enthalten[0].Id;
        }

        var aehnlich = liste
            .OrderBy(k => k.Name.Contains(gesucht, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(k => Abstand(k.Name.ToLowerInvariant(), gesucht.ToLowerInvariant()))
            .Take(5)
            .Select(k => $"„{k.Name}“")
            .ToList();
        string vorschlaege = aehnlich.Count == 0 ? "" : $" Ähnliche Namen: {string.Join(", ", aehnlich)}.";
        throw new McpException(enthalten.Count > 1 || exakt.Count > 1
            ? $"{art} „{gesucht}“ ist nicht eindeutig.{vorschlaege}"
            : $"{art} „{gesucht}“ nicht gefunden.{vorschlaege}");
    }

    /// <summary>Levenshtein-Abstand.</summary>
    private static int Abstand(string a, string b)
    {
        var vorher = new int[b.Length + 1];
        var aktuell = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            vorher[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            aktuell[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int kosten = a[i - 1] == b[j - 1] ? 0 : 1;
                aktuell[j] = Math.Min(Math.Min(aktuell[j - 1] + 1, vorher[j] + 1), vorher[j - 1] + kosten);
            }

            (vorher, aktuell) = (aktuell, vorher);
        }

        return vorher[b.Length];
    }
}
