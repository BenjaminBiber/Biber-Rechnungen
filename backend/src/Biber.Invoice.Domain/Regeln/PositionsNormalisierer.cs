using System.Text;
using System.Text.RegularExpressions;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>Normalisiert Positionstexte, damit gleiche Artikel eines Lieferanten denselben Schlüssel bekommen.</summary>
public static partial class PositionsNormalisierer
{
    private const int MaxAnzeigename = 200;

    /// <summary>
    /// Unicode-NFKC, Kleinbuchstaben, führende Artikel-/Positionsnummer (ab vier Ziffern) entfernt, Satzzeichen am Rand
    /// entfernt, Leerraum zusammengefasst. Mengen- und Größenangaben bleiben, sie unterscheiden Artikel.
    /// </summary>
    public static string Normalisieren(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var ergebnis = text.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        ergebnis = Leerraum().Replace(ergebnis, " ").Trim();
        ergebnis = Artikelnummer().Replace(ergebnis, "");
        ergebnis = RandOhneSatzzeichen(ergebnis);
        return Leerraum().Replace(ergebnis, " ").Trim();
    }

    /// <summary>Getrimmter Text, höchstens 200 Zeichen.</summary>
    public static string Anzeigename(string text)
    {
        var getrimmt = (text ?? "").Trim();
        return getrimmt.Length <= MaxAnzeigename ? getrimmt : getrimmt[..MaxAnzeigename].TrimEnd();
    }

    private static string RandOhneSatzzeichen(string text)
    {
        var start = 0;
        var ende = text.Length;
        while (start < ende && IstRandzeichen(text[start]))
        {
            start++;
        }

        while (ende > start && IstRandzeichen(text[ende - 1]))
        {
            ende--;
        }

        return text[start..ende];
    }

    /// <summary>Leerraum und Satzzeichen außer Klammern (sie gehören zum Text) und Prozent.</summary>
    private static bool IstRandzeichen(char c) =>
        char.IsWhiteSpace(c)
        || (char.IsPunctuation(c) && c != '%'
            && char.GetUnicodeCategory(c) is not (System.Globalization.UnicodeCategory.OpenPunctuation or System.Globalization.UnicodeCategory.ClosePunctuation))
        || c is '*' or '+' or '|' or '~';

    [GeneratedRegex(@"\s+")]
    private static partial Regex Leerraum();

    [GeneratedRegex(@"^\d{4,}[\s\-:]+")]
    private static partial Regex Artikelnummer();
}
