using System.Globalization;
using System.Text.RegularExpressions;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>Grundpreis einer Position, z. B. 13,98 je „kg“.</summary>
public sealed record Grundpreis(decimal Preis, string Einheit);

/// <summary>Berechnet den Grundpreis einer Position aus Einheit, Text oder dem Umrechnungsfaktor zum Stammartikel.</summary>
public static partial class GrundpreisRechner
{
    private const string Stueck = "Stück";

    /// <summary>Basiseinheit und Faktor zur Basiseinheit je erkannter Einheit (Kurzzeichen und UN/ECE-Codes).</summary>
    private static readonly Dictionary<string, (string Basis, decimal Faktor)> Masse = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kg"] = ("kg", 1m),
        ["kgm"] = ("kg", 1m),
        ["g"] = ("kg", 0.001m),
        ["grm"] = ("kg", 0.001m),
        ["l"] = ("l", 1m),
        ["ltr"] = ("l", 1m),
        ["ml"] = ("l", 0.001m),
        ["mlt"] = ("l", 0.001m),
        ["cl"] = ("l", 0.01m),
        ["m"] = ("m", 1m),
        ["mtr"] = ("m", 1m),
        ["cm"] = ("m", 0.01m),
        ["cmt"] = ("m", 0.01m),
        ["mm"] = ("m", 0.001m),
        ["mmt"] = ("m", 0.001m),
    };

    private static readonly HashSet<string> StueckEinheiten = new(StringComparer.OrdinalIgnoreCase)
    {
        "stück", "stueck", "st", "st.", "stk", "stk.", "c62", "h87", "pcs", "pc", "ea",
    };

    /// <summary>
    /// Mit <paramref name="umrechnungFaktor"/>: <c>Einzelpreis / Faktor × Grundpreismenge</c> je Basiseinheit des Stammartikels.
    /// Sonst aus Maßeinheit der Position oder Größenangabe im Text („500 g“, „6 × 1,5 l“) je kg, l oder m;
    /// Stück-Einheiten ergeben den Einzelpreis je Stück. Ohne Einzelpreis oder erkennbare Größe <c>null</c>.
    /// </summary>
    public static Grundpreis? Berechnen(
        string text,
        decimal? menge,
        string? einheit,
        decimal? einzelpreis,
        decimal? umrechnungFaktor = null,
        decimal grundpreisMenge = 1,
        string? basiseinheit = null)
    {
        if (einzelpreis is null || menge is <= 0)
        {
            return null;
        }

        if (umrechnungFaktor is > 0 && grundpreisMenge > 0 && !string.IsNullOrWhiteSpace(basiseinheit))
        {
            return new Grundpreis(
                StammartikelRechner.Grundpreis(einzelpreis, umrechnungFaktor, grundpreisMenge)!.Value,
                StammartikelRechner.GrundpreisEinheit(grundpreisMenge, basiseinheit.Trim()));
        }

        var e = einheit?.Trim() ?? "";
        if (Masse.TryGetValue(e, out var mass))
        {
            return new Grundpreis(Runden(einzelpreis.Value / mass.Faktor), mass.Basis);
        }

        if (AusText(text) is { } groesse)
        {
            return new Grundpreis(Runden(einzelpreis.Value / groesse.Menge), groesse.Basis);
        }

        return StueckEinheiten.Contains(e) ? new Grundpreis(Runden(einzelpreis.Value), Stueck) : null;
    }

    /// <summary>Gesamtgröße aus dem Text in Basiseinheiten; die letzte Angabe zählt.</summary>
    private static (decimal Menge, string Basis)? AusText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var treffer = Groesse().Matches(text);
        if (treffer.Count == 0)
        {
            return null;
        }

        var m = treffer[^1];
        var (basis, faktor) = Masse[m.Groups["einheit"].Value];
        var wert = decimal.Parse(m.Groups["wert"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        var anzahl = m.Groups["anzahl"].Success ? decimal.Parse(m.Groups["anzahl"].Value, CultureInfo.InvariantCulture) : 1m;
        var gesamt = anzahl * wert * faktor;
        return gesamt > 0 ? (gesamt, basis) : null;
    }

    private static decimal Runden(decimal wert) => Math.Round(wert, 2, MidpointRounding.AwayFromZero);

    [GeneratedRegex(
        @"(?<![\w,.])(?:(?<anzahl>\d+)\s*[x×*]\s*)?(?<wert>\d+(?:[.,]\d+)?)\s*(?<einheit>kg|g|ml|cl|l|mm|cm|m)(?![\w/²³])",
        RegexOptions.IgnoreCase)]
    private static partial Regex Groesse();
}
