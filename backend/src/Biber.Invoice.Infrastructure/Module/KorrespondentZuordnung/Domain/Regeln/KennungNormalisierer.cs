using System.Text.RegularExpressions;

namespace Biber.Invoice.Infrastructure.Module.KorrespondentZuordnung.Domain.Regeln;

/// <summary>Vergleichsformen für USt-IdNr., IBAN, BIC und Namen bei der Korrespondenten-Zuordnung.</summary>
public static partial class KennungNormalisierer
{
    private static readonly HashSet<string> Rechtsformen =
        ["gmbh", "kg", "ag", "ohg", "gbr", "ug", "ek", "ev", "co", "mbh", "se", "kgaa", "ltd", "inc", "haftungsbeschraenkt"];

    /// <summary>Großbuchstaben ohne Leer- und Sonderzeichen: „DE 284 117 560“ → <c>DE284117560</c>; leer → <c>null</c>.</summary>
    public static string? UstIdNr(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
        {
            return null;
        }

        var text = string.Concat(wert.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Großbuchstaben ohne Leerzeichen; leer → <c>null</c>.</summary>
    public static string? Iban(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
        {
            return null;
        }

        return string.Concat(wert.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    }

    /// <summary>Großbuchstaben ohne Leerzeichen; leer oder kein BIC (8 oder 11 Stellen nach ISO 9362) → <c>null</c>.</summary>
    public static string? Bic(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
        {
            return null;
        }

        var text = string.Concat(wert.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        return BicMuster().IsMatch(text) ? text : null;
    }

    /// <summary>Kleinbuchstaben, Umlaute umschrieben, ohne Rechtsform, Satzzeichen und Mehrfach-Leerzeichen.</summary>
    public static string Name(string wert)
    {
        var text = wert.ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("&", " ", StringComparison.Ordinal)
            .Replace(".", "", StringComparison.Ordinal);
        var woerter = NichtAlphanumerisch().Split(text)
            .Where(w => w.Length > 0 && !Rechtsformen.Contains(w));
        return string.Join(' ', woerter);
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NichtAlphanumerisch();

    [GeneratedRegex(@"^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex BicMuster();
}
