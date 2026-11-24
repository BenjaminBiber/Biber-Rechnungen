using System.Globalization;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>Zerlegt einen Zeitraum in Voranmeldungszeiträume (Monat, Quartal, Jahr), begrenzt auf die Auswahl.</summary>
public static class UstvaZeitraumRechner
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Alle Voranmeldungszeiträume, die [<paramref name="von"/>, <paramref name="bis"/>] berühren. Angeschnittene
    /// Zeiträume beginnen bzw. enden an der Auswahlgrenze und behalten ihre Bezeichnung („Q3 2026“, „Oktober 2026“, „2026“).
    /// </summary>
    public static IEnumerable<(DateOnly Von, DateOnly Bis, string Bezeichnung)> Zeitraeume(DateOnly von, DateOnly bis, UstvaZeitraum art)
    {
        if (von > bis)
        {
            yield break;
        }

        var monate = art switch
        {
            UstvaZeitraum.Monatlich => 1,
            UstvaZeitraum.Quartalsweise => 3,
            _ => 12,
        };
        var beginn = new DateOnly(von.Year, ((von.Month - 1) / monate * monate) + 1, 1);
        while (beginn <= bis)
        {
            var ende = beginn.AddMonths(monate).AddDays(-1);
            yield return (Max(beginn, von), Min(ende, bis), Bezeichnung(beginn, art));
            beginn = beginn.AddMonths(monate);
        }
    }

    private static string Bezeichnung(DateOnly beginn, UstvaZeitraum art) => art switch
    {
        UstvaZeitraum.Monatlich => beginn.ToString("MMMM yyyy", Deutsch),
        UstvaZeitraum.Quartalsweise => $"Q{((beginn.Month - 1) / 3) + 1} {beginn.Year}",
        _ => beginn.Year.ToString(CultureInfo.InvariantCulture),
    };

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
