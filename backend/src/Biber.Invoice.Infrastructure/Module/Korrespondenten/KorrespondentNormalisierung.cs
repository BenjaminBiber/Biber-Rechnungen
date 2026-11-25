using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Biber.Invoice.Infrastructure.Module.Korrespondenten;

/// <summary>Normalisierung und Formprüfung von Namen, Kennungen (USt-IdNr., IBAN, BIC) und Websites.</summary>
public static partial class KorrespondentNormalisierung
{
    private static readonly HashSet<string> Rechtsformen =
        ["gmbh", "kg", "ag", "ohg", "gbr", "ug", "ek", "ev", "co", "mbh", "se", "kgaa", "ltd", "inc", "haftungsbeschraenkt"];

    /// <summary>Kennung ohne Leerzeichen in Großbuchstaben; leer → <c>null</c>.</summary>
    public static string? Kennung(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
        {
            return null;
        }

        return string.Concat(wert.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    }

    /// <summary>Name für den Dubletten-Vergleich: klein, Umlaute umschrieben, ohne Rechtsform und Satzzeichen.</summary>
    public static string Name(string name)
    {
        var text = name.ToLowerInvariant()
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

    public static bool IstUstIdNr(string kennung) => UstIdNrMuster().IsMatch(kennung);

    /// <summary>Prüft Form und Prüfziffer (ISO 13616, mod 97).</summary>
    public static bool IstIban(string kennung)
    {
        if (!IbanMuster().IsMatch(kennung))
        {
            return false;
        }

        var umgestellt = string.Concat(kennung.AsSpan(4), kennung.AsSpan(0, 4));
        var ziffern = new StringBuilder();
        foreach (var c in umgestellt)
        {
            ziffern.Append(char.IsLetter(c) ? (c - 'A' + 10).ToString(System.Globalization.CultureInfo.InvariantCulture) : c.ToString());
        }

        return BigInteger.Parse(ziffern.ToString(), System.Globalization.CultureInfo.InvariantCulture) % 97 == 1;
    }

    /// <summary>Prüft die Form (ISO 9362): 8 oder 11 Stellen.</summary>
    public static bool IstBic(string kennung) => BicMuster().IsMatch(kennung);

    /// <summary>Host der Website ohne „www.“; <c>null</c>, wenn sich keiner ableiten lässt.</summary>
    public static string? Domain(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return null;
        }

        var text = website.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NichtAlphanumerisch();

    [GeneratedRegex(@"^[A-Z]{2}[A-Z0-9]{2,13}$")]
    private static partial Regex UstIdNrMuster();

    [GeneratedRegex(@"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex IbanMuster();

    [GeneratedRegex(@"^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex BicMuster();
}
