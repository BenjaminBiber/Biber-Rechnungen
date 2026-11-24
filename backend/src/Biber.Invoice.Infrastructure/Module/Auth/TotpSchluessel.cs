using System.Security.Cryptography;
using System.Text;

namespace Biber.Invoice.Infrastructure.Module.Auth;

/// <summary>TOTP-Schlüssel (Base32 nach RFC 4648, ohne Auffüllung) und der <c>otpauth://</c>-Link für Authenticator-Apps.</summary>
public static class TotpSchluessel
{
    public const string Aussteller = "Biber-Rechnungen";

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Neuer zufälliger Schlüssel aus 20 Byte (160 Bit, 32 Zeichen).</summary>
    public static string Erzeugen() => Kodieren(RandomNumberGenerator.GetBytes(20));

    /// <summary>
    /// Bringt einen eingegebenen Schlüssel in die Normalform (Großbuchstaben, ohne Leerzeichen und „=“).
    /// <c>null</c>, wenn er kein gültiges Base32 ist oder weniger als 80 Bit hat.
    /// </summary>
    public static string? Normalisieren(string? schluessel)
    {
        if (schluessel is null)
        {
            return null;
        }

        var normal = new string(schluessel.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '=').ToArray())
            .ToUpperInvariant();
        return normal.Length >= 16 && normal.All(c => Alphabet.Contains(c, StringComparison.Ordinal)) ? normal : null;
    }

    public static string OtpAuthUri(string email, string schluessel) =>
        $"otpauth://totp/{Aussteller}:{email}?secret={schluessel}&issuer={Aussteller}";

    private static string Kodieren(byte[] daten)
    {
        var text = new StringBuilder((daten.Length * 8 + 4) / 5);
        int puffer = 0, bits = 0;
        foreach (var b in daten)
        {
            puffer = (puffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                text.Append(Alphabet[(puffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            text.Append(Alphabet[(puffer << (5 - bits)) & 31]);
        }

        return text.ToString();
    }
}
