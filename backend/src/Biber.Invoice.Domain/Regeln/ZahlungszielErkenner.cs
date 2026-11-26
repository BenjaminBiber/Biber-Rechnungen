using System.Globalization;
using System.Text.RegularExpressions;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>
/// Liest das Fälligkeitsdatum aus den Zahlungsbedingungen im Text eines Belegs, z. B. „Bitte zahlen Sie innerhalb von
/// 14 Tagen“, „Zahlungsziel: 30 Tage netto“, „zahlbar bis 21.10.2026“ oder „zahlbar sofort“. Fristen in Tagen oder
/// Wochen zählen ab Rechnungsdatum. Skonto-Fristen gelten nicht als Zahlungsziel; stehen mehrere Fristen da, gilt die
/// späteste (das Nettoziel).
/// </summary>
public static partial class ZahlungszielErkenner
{
    public const int MaxTage = 120;

    private static readonly Dictionary<string, int> Zahlwoerter = new(StringComparer.OrdinalIgnoreCase)
    {
        ["einer"] = 1,
        ["zwei"] = 2,
        ["drei"] = 3,
        ["vier"] = 4,
        ["sechs"] = 6,
        ["sieben"] = 7,
        ["acht"] = 8,
        ["zehn"] = 10,
        ["vierzehn"] = 14,
        ["zwanzig"] = 20,
        ["einundzwanzig"] = 21,
        ["dreißig"] = 30,
        ["dreissig"] = 30,
        ["sechzig"] = 60,
        ["neunzig"] = 90,
    };

    private static readonly string[] Monate =
        ["jan", "feb", "mär", "apr", "mai", "jun", "jul", "aug", "sep", "okt", "nov", "dez"];

    /// <summary>
    /// Fälligkeitsdatum laut Text; <c>null</c>, wenn keine Zahlungsfrist erkennbar ist oder eine Frist in Tagen ohne
    /// Rechnungsdatum nicht in ein Datum umgerechnet werden kann.
    /// </summary>
    public static DateOnly? Faelligkeit(string? text, DateOnly? rechnungsdatum)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var zeilen = text.Split('\n', StringSplitOptions.TrimEntries);
        DateOnly? ergebnis = null;
        for (var i = 0; i < zeilen.Length; i++)
        {
            var frist = AusZeile(zeilen[i], rechnungsdatum);

            // Umbrochener Satz („Bitte überweisen Sie den Betrag innerhalb von⏎14 Tagen“): mit der Folgezeile lesen.
            if (frist is null && i + 1 < zeilen.Length && ZahlungsWort().IsMatch(zeilen[i]) && !SatzEnde().IsMatch(zeilen[i]))
            {
                frist = AusZeile($"{zeilen[i]} {zeilen[i + 1]}", rechnungsdatum);
            }

            ergebnis = Spaeter(ergebnis, frist);
        }

        return ergebnis;
    }

    private static DateOnly? AusZeile(string zeile, DateOnly? rechnungsdatum)
    {
        DateOnly? ergebnis = null;
        foreach (var satz in SatzTrenner().Split(zeile))
        {
            if (!ZahlungsWort().IsMatch(satz) && !NettoFrist().IsMatch(satz))
            {
                continue;
            }

            // Die Skonto-Frist ist kürzer als das Zahlungsziel; Fristen bis dahin gehören zum Skonto
            // („Zahlbar innerhalb 8 Tagen, 2 % Skonto“).
            var skontoBis = SkontoErkenner.AusText(satz) is { } skonto ? SkontoErkenner.Bis(skonto, rechnungsdatum) : null;

            foreach (var klausel in KlauselTrenner().Split(satz))
            {
                if (IstSkonto(klausel) || AusschlussMuster().IsMatch(klausel))
                {
                    continue;
                }

                foreach (var datum in Fristen(klausel, rechnungsdatum).Where(d => skontoBis is null || d > skontoBis))
                {
                    ergebnis = Spaeter(ergebnis, datum);
                }
            }
        }

        return ergebnis;
    }

    private static IEnumerable<DateOnly> Fristen(string klausel, DateOnly? rechnungsdatum)
    {
        // Ein festes Datum zählt nur mit Zahlungsbezug in derselben Klausel, damit „Leistung vom … bis …“ nicht greift.
        if (ZahlungsWort().IsMatch(klausel) || NettoWort().IsMatch(klausel))
        {
            foreach (Match m in FristDatumMuster().Matches(klausel))
            {
                if (Datum(m) is { } datum
                    && (rechnungsdatum is not { } r || (datum >= r && datum <= r.AddYears(1))))
                {
                    yield return datum;
                }
            }
        }

        if (rechnungsdatum is not { } rechnung)
        {
            yield break;
        }

        foreach (Match m in TageMuster().Matches(klausel))
        {
            if (Anzahl(m.Groups["anzahl"].Value) is { } tage and > 0 and <= MaxTage)
            {
                yield return rechnung.AddDays(tage);
            }
        }

        foreach (Match m in WochenMuster().Matches(klausel))
        {
            if (Anzahl(m.Groups["anzahl"].Value) * 7 is { } tage and > 0 and <= MaxTage)
            {
                yield return rechnung.AddDays(tage);
            }
        }

        if (SofortMuster().IsMatch(klausel))
        {
            yield return rechnung;
        }
    }

    /// <summary>Klausel mit Skonto („2 % Skonto“, „14 Tage 2 %“), aber nicht „ohne Skonto“.</summary>
    private static bool IstSkonto(string klausel) =>
        (SkontoWort().IsMatch(klausel) && !OhneSkonto().IsMatch(klausel))
        || ProzentMuster().Matches(klausel).Any(m =>
            decimal.TryParse(m.Groups["wert"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var p)
            && p is > 0m and <= SkontoErkenner.MaxProzent);

    private static DateOnly? Spaeter(DateOnly? a, DateOnly? b) => a is null || b > a ? b : a;

    private static int? Anzahl(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var zahl) ? zahl
        : Zahlwoerter.TryGetValue(text, out var wort) ? wort
        : null;

    private static DateOnly? Datum(Match m)
    {
        var tag = int.Parse(m.Groups["tag"].Value, CultureInfo.InvariantCulture);
        var monat = m.Groups["monat"].Success
            ? int.Parse(m.Groups["monat"].Value, CultureInfo.InvariantCulture)
            : Array.FindIndex(Monate, n => m.Groups["monatsname"].Value.Replace("ae", "ä", StringComparison.OrdinalIgnoreCase)
                .StartsWith(n, StringComparison.OrdinalIgnoreCase)) + 1;
        var jahr = int.Parse(m.Groups["jahr"].Value, CultureInfo.InvariantCulture);
        if (jahr < 100)
        {
            jahr += 2000;
        }

        return monat is >= 1 and <= 12 && jahr is >= 2000 and <= 2100 && tag >= 1 && tag <= DateTime.DaysInMonth(jahr, monat)
            ? new DateOnly(jahr, monat, tag)
            : null;
    }

    [GeneratedRegex(@"zahl(?:bar|en|ung)|überweis|ueberweis|begleich|fällig|faellig|rechnungsbetrag", RegexOptions.IgnoreCase)]
    private static partial Regex ZahlungsWort();

    /// <summary>„30 Tage netto“ steht oft ohne Zahlungswort unter „Zahlungsbedingungen:“.</summary>
    [GeneratedRegex(@"(?<![a-zäöüß])tage?n?\s+(?:rein\s+)?netto\b|\bnetto\s+(?:kasse\s+)?(?:innerhalb\s+(?:von\s+)?)?\d{1,3}\s*tag", RegexOptions.IgnoreCase)]
    private static partial Regex NettoFrist();

    [GeneratedRegex(@"\bnetto\b|ohne\s+abzug|ohne\s+skonto", RegexOptions.IgnoreCase)]
    private static partial Regex NettoWort();

    [GeneratedRegex(
        @"verzug|mahn|widerruf|rückgabe|rueckgabe|rücksend|ruecksend|umtausch|gewährleistung|gewaehrleistung|garantie|reklamation|beanstand|einspruch|kündig|kuendig|lieferzeit|lieferfrist|aufbewahr",
        RegexOptions.IgnoreCase)]
    private static partial Regex AusschlussMuster();

    [GeneratedRegex(@"[.!?]$")]
    private static partial Regex SatzEnde();

    /// <summary>Satzenden und „;“; der Punkt eines Tagesdatums („21. Oktober“) trennt nicht.</summary>
    [GeneratedRegex(@";|(?<!(?:^|[^\d.])\d{1,2})[.!?](?=\s+[A-ZÄÖÜ])")]
    private static partial Regex SatzTrenner();

    /// <summary>Kommas außerhalb von Zahlen, „oder“, „sonst“, „danach“, „andernfalls“, „bzw.“.</summary>
    [GeneratedRegex(@"(?<!\d),|,(?!\d)|\s(?:oder|sonst|danach|andernfalls|bzw\.)\s", RegexOptions.IgnoreCase)]
    private static partial Regex KlauselTrenner();

    [GeneratedRegex(@"skonto", RegexOptions.IgnoreCase)]
    private static partial Regex SkontoWort();

    [GeneratedRegex(@"\b(?:ohne|kein(?:en)?)\s+(?:\w+\s+)?skonto", RegexOptions.IgnoreCase)]
    private static partial Regex OhneSkonto();

    [GeneratedRegex(@"(?<!\d)(?<wert>\d{1,2}(?:[.,]\d{1,2})?)\s?(?:%|prozent)", RegexOptions.IgnoreCase)]
    private static partial Regex ProzentMuster();

    [GeneratedRegex(
        @"(?:(?<!\d)(?<anzahl>\d{1,3})\s*|\b(?<anzahl>sieben|acht|zehn|vierzehn|zwanzig|einundzwanzig|dreißig|dreissig|sechzig|neunzig)\s+)(?:Kalender|Werk|Arbeits)?tag(?:e|en)?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex TageMuster();

    [GeneratedRegex(@"(?:(?<!\d)(?<anzahl>\d{1,2})\s*|\b(?<anzahl>einer|zwei|drei|vier|sechs|acht)\s+)Wochen?\b", RegexOptions.IgnoreCase)]
    private static partial Regex WochenMuster();

    [GeneratedRegex(
        @"\b(?:zahlbar|fällig|faellig|zahlung(?:sziel|sbedingungen)?|zahlen)\b\W*(?:\w+\W+)?sofort\b|\bsofort\b\W*(?:\w+\W+){0,4}?(?:zahlbar|fällig|faellig|zu\s+(?:be)?zahlen)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SofortMuster();

    /// <summary>Datum mit Fristwort davor: „bis (zum) 21.10.2026“, „fällig am 21. Oktober 2026“, „Zahlungsziel: 21.10.26“.</summary>
    [GeneratedRegex(
        @"\b(?:bis|zum|am|spätestens|spaetestens|fällig\w*|faellig\w*|zahlungsziel|zahlungsfrist|zahlungstermin|zahlbar)\s*:?\s*(?:(?:zum|den|dem|am|spätestens|spaetestens)\s+)*(?<tag>\d{1,2})\.\s?(?:(?<monat>\d{1,2})\.\s?|(?<monatsname>Januar|Jan|Februar|Feb|März|Maerz|Mär|April|Apr|Mai|Juni|Jun|Juli|Jul|August|Aug|September|Sept|Sep|Oktober|Okt|November|Nov|Dezember|Dez)\.?\s+)(?<jahr>\d{4}|\d{2})(?!\d)",
        RegexOptions.IgnoreCase)]
    private static partial Regex FristDatumMuster();
}
