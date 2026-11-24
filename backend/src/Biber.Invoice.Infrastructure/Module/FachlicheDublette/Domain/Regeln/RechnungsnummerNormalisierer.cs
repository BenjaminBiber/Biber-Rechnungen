namespace Biber.Invoice.Infrastructure.Module.FachlicheDublette.Domain.Regeln;

/// <summary>Vergleichsform für Rechnungsnummern bei der Erkennung fachlicher Dubletten.</summary>
public static class RechnungsnummerNormalisierer
{
    /// <summary>
    /// Großbuchstaben ohne Leerzeichen, Bindestriche, Punkte und Schrägstriche:
    /// „2026-48817“ und „2026 48817“ → <c>202648817</c>; leer → <c>null</c>.
    /// </summary>
    public static string? Normalisieren(string? nr)
    {
        if (string.IsNullOrWhiteSpace(nr))
        {
            return null;
        }

        var text = string.Concat(nr.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '.' or '/' or '\\')))
            .ToUpperInvariant();
        return text.Length == 0 ? null : text;
    }
}
