using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>
/// Bereinigt Merkmale (Größe, Farbe …) aus Belegen und vereinheitlicht ihre Namen, damit „Gr.“, „Size“ und „Größe“
/// dieselbe Spalte ergeben.
/// </summary>
public static partial class MerkmalNormalisierer
{
    /// <summary>Höchstlänge eines Merkmalnamens.</summary>
    public const int MaxName = 50;

    /// <summary>Höchstlänge eines Merkmalwerts.</summary>
    public const int MaxWert = 100;

    /// <summary>Höchstzahl der Merkmale einer Position.</summary>
    public const int MaxAnzahl = 20;

    /// <summary>Übliche Schreibweisen (klein, ohne Punkt am Ende) und ihr einheitlicher Name.</summary>
    private static readonly Dictionary<string, string> Namen = new(StringComparer.Ordinal)
    {
        ["größe"] = "Größe",
        ["grösse"] = "Größe",
        ["groesse"] = "Größe",
        ["gr"] = "Größe",
        ["size"] = "Größe",
        ["farbe"] = "Farbe",
        ["color"] = "Farbe",
        ["colour"] = "Farbe",
        ["ausführung"] = "Ausführung",
        ["ausfuehrung"] = "Ausführung",
        ["länge"] = "Länge",
        ["laenge"] = "Länge",
        ["length"] = "Länge",
        ["variante"] = "Variante",
    };

    /// <summary>
    /// Bereinigte Merkmale in der gegebenen Reihenfolge: Text wie in <see cref="Wert"/> und <see cref="Name"/>, ohne
    /// leere Werte und Doppelte, höchstens <see cref="MaxAnzahl"/>.
    /// </summary>
    public static List<Merkmal> Bereinigen(IEnumerable<(string? Name, string? Wert)>? merkmale) =>
        [.. (merkmale ?? [])
            .Select(m => Wert(m.Wert) is { } wert ? new Merkmal(Name(m.Name), wert) : null)
            .OfType<Merkmal>()
            .Distinct()
            .Take(MaxAnzahl)];

    /// <summary>Wie oben, für schon vorhandene Merkmale (Extraktion, Bearbeitung).</summary>
    public static List<Merkmal> Bereinigen(IEnumerable<Merkmal>? merkmale) =>
        Bereinigen(merkmale?.Select(m => (m.Name, (string?)m.Wert)));

    /// <summary>
    /// Einheitlicher Name: Unicode-NFKC, Leerraum zusammengefasst, Doppelpunkt am Ende entfernt („Paketgröße:“),
    /// bekannte Schreibweisen vereinheitlicht („Gr.“ → „Größe“, auch in „Gr./Farbe“), erster Buchstabe groß.
    /// <c>null</c> ohne Buchstaben oder Ziffern.
    /// </summary>
    public static string? Name(string? name)
    {
        var text = Text(name)?.TrimEnd(':', ' ');
        if (text is null || !text.Any(char.IsLetterOrDigit))
        {
            return null;
        }

        var teile = text.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return Kuerzen(string.Join('/', teile.Select(Einheitlich)), MaxName);
    }

    /// <summary>Wert: Unicode-NFKC, Leerraum zusammengefasst, höchstens <see cref="MaxWert"/> Zeichen.</summary>
    public static string? Wert(string? wert)
    {
        var text = Text(wert);
        return text is not null && text.Any(char.IsLetterOrDigit) ? Kuerzen(text, MaxWert) : null;
    }

    private static string Einheitlich(string teil)
    {
        if (Namen.TryGetValue(teil.TrimEnd('.').ToLower(CultureInfo.InvariantCulture), out var name))
        {
            return name;
        }

        return char.IsLower(teil[0]) ? char.ToUpper(teil[0], CultureInfo.InvariantCulture) + teil[1..] : teil;
    }

    private static string? Text(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Leerraum().Replace(text.Normalize(NormalizationForm.FormKC), " ").Trim();

    private static string Kuerzen(string text, int laenge) => text.Length <= laenge ? text : text[..laenge].TrimEnd();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Leerraum();
}
