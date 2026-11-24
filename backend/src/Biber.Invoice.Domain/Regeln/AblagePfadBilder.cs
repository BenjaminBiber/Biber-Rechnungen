using System.Globalization;
using System.Text;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>Bildet den relativen Ablagepfad <c>{Jahr}/{Korrespondent}/{Datum}_{Rechnungsnr}.{ext}</c> (Plan 6, Schritt 9).</summary>
public static class AblagePfadBilder
{
    private const int MaxLaenge = 60;

    public static string Bilden(int jahr, string? korrespondent, DateOnly datum, string? rechnungsnummer, string endung)
    {
        var ordner = Bereinigen(korrespondent) is { Length: > 0 } k ? k : "_Unbekannt";
        var nummer = Bereinigen(rechnungsnummer) is { Length: > 0 } n ? n : "ohne-Nummer";
        var ext = Bereinigen(endung).ToLowerInvariant();
        var datei = $"{datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}_{nummer}";
        return string.IsNullOrEmpty(ext)
            ? $"{jahr.ToString("D4", CultureInfo.InvariantCulture)}/{ordner}/{datei}"
            : $"{jahr.ToString("D4", CultureInfo.InvariantCulture)}/{ordner}/{datei}.{ext}";
    }

    /// <summary>Umlaute ausschreiben, alles außer <c>[A-Za-z0-9-]</c> zu <c>_</c>, Folgen zusammenfassen, Rand kürzen.</summary>
    internal static string Bereinigen(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
        {
            return "";
        }

        var sb = new StringBuilder(wert.Length);
        foreach (var zeichen in wert.Trim())
        {
            var ersatz = zeichen switch
            {
                'Ä' => "Ae",
                'Ö' => "Oe",
                'Ü' => "Ue",
                'ä' => "ae",
                'ö' => "oe",
                'ü' => "ue",
                'ß' => "ss",
                _ when char.IsAsciiLetterOrDigit(zeichen) || zeichen == '-' => zeichen.ToString(),
                _ => "_",
            };
            foreach (var c in ersatz)
            {
                if (c != '_' || sb.Length == 0 || sb[^1] != '_')
                {
                    sb.Append(c);
                }
            }
        }

        var ergebnis = sb.ToString().Trim('_');
        if (ergebnis.Length > MaxLaenge)
        {
            ergebnis = ergebnis[..MaxLaenge].TrimEnd('_');
        }

        return ergebnis;
    }
}
