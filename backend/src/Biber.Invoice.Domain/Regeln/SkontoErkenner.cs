using System.Globalization;
using System.Text.RegularExpressions;
using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>
/// Liest Skonto aus dem Text eines Belegs: zuerst die XRechnung-Syntax <c>#SKONTO#TAGE=14#PROZENT=2.00#</c>, dann
/// deutsche Zahlungsbedingungen wie „14 Tage 2 % Skonto“ oder „bis 12.12.2026 abzüglich 3 % Skonto“.
/// </summary>
public static partial class SkontoErkenner
{
    /// <summary>Höchster Prozentsatz, der als Skonto gilt; größere Werte sind fast immer Steuersätze.</summary>
    public const decimal MaxProzent = 10m;

    public const int MaxTage = 120;

    /// <summary>Erste Skonto-Angabe im Text; <c>null</c>, wenn keine erkennbar ist oder Skonto ausgeschlossen wird.</summary>
    public static SkontoAngabe? AusText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var xrechnung = XRechnungMuster().Match(text);
        if (xrechnung.Success
            && int.TryParse(xrechnung.Groups["tage"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var xTage)
            && Prozent(xrechnung.Groups["prozent"].Value) is { } xProzent
            && Plausibel(xProzent, xTage))
        {
            return new SkontoAngabe(xProzent, xTage, null);
        }

        var abschnitte = AbschnittTrenner().Split(text);
        for (var i = 0; i < abschnitte.Length; i++)
        {
            var abschnitt = abschnitte[i];
            var schluessel = SkontoWort().Match(abschnitt);
            if (!schluessel.Success || AusschlussMuster().IsMatch(abschnitt))
            {
                continue;
            }

            var prozent = ProzentMuster().Matches(abschnitt)
                .Select(m => (Wert: Prozent(m.Groups["wert"].Value), Abstand: Abstand(m, schluessel)))
                .Where(p => p.Wert is > 0m and <= MaxProzent)
                .OrderBy(p => p.Abstand)
                .Select(p => p.Wert)
                .FirstOrDefault();
            if (prozent is not { } p)
            {
                continue;
            }

            // Die Frist steht meist in derselben Klausel, manchmal davor („14 Tagen, 2 % Skonto“) oder danach.
            // Nachbarklauseln mit dem Nettoziel („30 Tage netto“) gehören nicht zum Skonto.
            var frist = Frist(abschnitt, schluessel);
            if (frist is null && i > 0 && !NettoMuster().IsMatch(abschnitte[i - 1]))
            {
                frist = Frist(abschnitte[i - 1], null);
            }

            if (frist is null && i + 1 < abschnitte.Length && !NettoMuster().IsMatch(abschnitte[i + 1]))
            {
                frist = Frist(abschnitte[i + 1], null);
            }

            if (frist is { } f)
            {
                return new SkontoAngabe(p, f.Tage, f.Bis);
            }
        }

        return null;
    }

    private static (int? Tage, DateOnly? Bis)? Frist(string abschnitt, Match? schluessel)
    {
        var datum = DatumMuster().Matches(abschnitt)
            .Select(m => (Wert: Datum(m), Abstand: schluessel is null ? 0 : Abstand(m, schluessel)))
            .Where(d => d.Wert is not null)
            .OrderBy(d => d.Abstand)
            .Select(d => d.Wert)
            .FirstOrDefault();
        if (datum is not null)
        {
            return (null, datum);
        }

        var tage = TageMuster().Matches(abschnitt)
            .Select(m => (Wert: int.Parse(m.Groups["tage"].Value, CultureInfo.InvariantCulture), Abstand: schluessel is null ? 0 : Abstand(m, schluessel)))
            .Where(t => t.Wert is > 0 and <= MaxTage)
            .OrderBy(t => t.Abstand)
            .Select(t => (int?)t.Wert)
            .FirstOrDefault();
        return tage is null ? null : (tage, null);
    }

    /// <summary>Letzter Skonto-Tag: festes Datum oder Rechnungsdatum + Tage; ohne Rechnungsdatum unbekannt.</summary>
    public static DateOnly? Bis(SkontoAngabe angabe, DateOnly? rechnungsdatum) =>
        angabe.Bis ?? (angabe.Tage is { } tage && rechnungsdatum is { } datum ? datum.AddDays(tage) : null);

    /// <summary>Ersparnis in Belegwährung, auf Cent gerundet.</summary>
    public static decimal? Betrag(decimal? brutto, decimal? prozent) =>
        brutto is { } b && prozent is { } p ? Math.Round(Math.Abs(b) * p / 100m, 2, MidpointRounding.AwayFromZero) : null;

    public static bool Plausibel(decimal prozent, int? tage) =>
        prozent is > 0m and <= MaxProzent && tage is null or (> 0 and <= MaxTage);

    private static int Abstand(Match m, Match schluessel) =>
        m.Index >= schluessel.Index + schluessel.Length
            ? m.Index - (schluessel.Index + schluessel.Length)
            : Math.Max(0, schluessel.Index - (m.Index + m.Length));

    private static decimal? Prozent(string text) =>
        decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;

    private static DateOnly? Datum(Match m)
    {
        var tag = int.Parse(m.Groups["tag"].Value, CultureInfo.InvariantCulture);
        var monat = int.Parse(m.Groups["monat"].Value, CultureInfo.InvariantCulture);
        var jahr = int.Parse(m.Groups["jahr"].Value, CultureInfo.InvariantCulture);
        if (jahr < 100)
        {
            jahr += 2000;
        }

        return monat is >= 1 and <= 12 && jahr is >= 2000 and <= 2100 && tag >= 1 && tag <= DateTime.DaysInMonth(jahr, monat)
            ? new DateOnly(jahr, monat, tag)
            : null;
    }

    [GeneratedRegex(@"#SKONTO#TAGE=(?<tage>\d{1,3})#PROZENT=(?<prozent>\d{1,2}(?:[.,]\d{1,4})?)#", RegexOptions.IgnoreCase)]
    private static partial Regex XRechnungMuster();

    [GeneratedRegex(@"\b(?:ohne|kein(?:en)?)\s+(?:\w+\s+)?skonto|skonto\s+(?:wird\s+|ist\s+)?(?:nicht|ausgeschlossen)|nicht\s+skontierf", RegexOptions.IgnoreCase)]
    private static partial Regex AusschlussMuster();

    [GeneratedRegex(@"\bnetto\b|ohne\s+abzug|\brein\b", RegexOptions.IgnoreCase)]
    private static partial Regex NettoMuster();

    /// <summary>Trennt Klauseln: Zeilen, „;“, Kommas außerhalb von Zahlen, „oder“, „sonst“ und Satzenden.</summary>
    [GeneratedRegex(@"\r?\n|;|(?<!\d),|,(?!\d)|\s(?:oder|sonst|danach)\s|\.(?=\s+[A-ZÄÖÜ])", RegexOptions.IgnoreCase)]
    private static partial Regex AbschnittTrenner();

    [GeneratedRegex(@"skonto", RegexOptions.IgnoreCase)]
    private static partial Regex SkontoWort();

    [GeneratedRegex(@"(?<!\d)(?<wert>\d{1,2}(?:[.,]\d{1,2})?)\s?(?:%|prozent)", RegexOptions.IgnoreCase)]
    private static partial Regex ProzentMuster();

    [GeneratedRegex(@"(?<!\d)(?<tage>\d{1,3})\s*(?:Kalender|Werk|Arbeits)?tag(?:e|en)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TageMuster();

    [GeneratedRegex(@"(?<!\d)(?<tag>\d{1,2})\.(?<monat>\d{1,2})\.(?<jahr>\d{4}|\d{2})(?!\d)")]
    private static partial Regex DatumMuster();
}
