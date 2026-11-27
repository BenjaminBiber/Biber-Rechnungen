using System.Text;
using System.Text.RegularExpressions;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>Bereinigt Artikelnummern aus Belegen.</summary>
public static partial class ArtikelnummerNormalisierer
{
    /// <summary>Höchstlänge einer Artikelnummer.</summary>
    public const int MaxLaenge = 100;

    /// <summary>
    /// Schlüssel für die Artikelzuordnung: wie <see cref="Bereinigen"/>, dazu ohne Leerraum und in Großbuchstaben
    /// („ab 12-3“ und „AB12-3“ sind dieselbe Nummer). <c>null</c> ohne Buchstaben oder Ziffern.
    /// </summary>
    public static string? Normalisieren(string? nummer)
    {
        var text = Bereinigen(nummer);
        return text is null ? null : Kuerzen(Leerraum().Replace(text, "").ToUpperInvariant());
    }

    /// <summary>
    /// Artikelnummer zur Anzeige: Unicode-NFKC, Leerraum zusammengefasst, ein vorangestelltes „Art.-Nr.:“ entfernt,
    /// höchstens <see cref="MaxLaenge"/> Zeichen. <c>null</c> ohne Buchstaben oder Ziffern.
    /// </summary>
    public static string? Bereinigen(string? nummer)
    {
        var text = Text(nummer);
        return text is null ? null : MitInhalt(Praefix().Replace(text, "").Trim());
    }

    private static string? Text(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Leerraum().Replace(text.Normalize(NormalizationForm.FormKC), " ").Trim();

    private static string? MitInhalt(string? text) =>
        text is not null && text.Any(char.IsLetterOrDigit) ? Kuerzen(text) : null;

    private static string Kuerzen(string text) => text.Length <= MaxLaenge ? text : text[..MaxLaenge].TrimEnd();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Leerraum();

    /// <summary>„Art.-Nr.“, „Artikel-Nr:“, „Artikelnummer“, „Art. #“ vor der eigentlichen Nummer.</summary>
    [GeneratedRegex(@"^art(?:ikel)?\.?\s*-?\s*(?:nr\.?|nummer|no\.?|#)\s*:?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex Praefix();
}
