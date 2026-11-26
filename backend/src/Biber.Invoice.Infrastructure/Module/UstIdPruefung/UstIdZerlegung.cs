namespace Biber.Invoice.Infrastructure.Module.UstIdPruefung;

/// <summary>Zerlegt eine USt-IdNr. in Länderpräfix und Nummer, wie VIES sie erwartet.</summary>
public static class UstIdZerlegung
{
    /// <summary>Präfixe der Mitgliedstaaten in VIES; Griechenland heißt „EL“, Nordirland „XI“.</summary>
    private static readonly HashSet<string> ViesLaender =
    [
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "EL", "ES", "FI", "FR", "HR", "HU", "IE", "IT",
        "LT", "LU", "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK", "XI",
    ];

    /// <summary>
    /// <c>(Land, Nummer)</c> für VIES; <c>null</c>, wenn die Kennung keinem Mitgliedstaat gehört (z. B. CH, GB) oder zu kurz ist.
    /// </summary>
    public static (string Land, string Nummer)? Zerlegen(string? ustIdNr)
    {
        var kennung = string.Concat((ustIdNr ?? "").Where(char.IsLetterOrDigit)).ToUpperInvariant();
        if (kennung.Length < 4 || !char.IsAsciiLetter(kennung[0]) || !char.IsAsciiLetter(kennung[1]))
        {
            return null;
        }

        var land = kennung[..2] == "GR" ? "EL" : kennung[..2];
        return ViesLaender.Contains(land) ? (land, kennung[2..]) : null;
    }
}
