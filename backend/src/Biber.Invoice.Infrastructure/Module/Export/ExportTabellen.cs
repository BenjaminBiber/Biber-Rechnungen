using System.Globalization;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.Infrastructure.Module.Export;

/// <summary>Summen einer Kategorie über die Euro-Belege.</summary>
public sealed record KategorieSumme(string Kategorie, string? EuerZeile, int Anzahl, decimal Netto, decimal Steuer, decimal Brutto);

/// <summary>
/// Baut die Tabellen eines Exports: Belegliste, Summen je Kategorie und Vorsteuer je UStVA-Zeitraum.
/// Beträge zählen nur aus Euro-Belegen; Fremdwährungsbelege stehen mit ihrer Währung in der Belegliste.
/// Ein Zahlungsdatum gibt es bewusst nicht (R4-5).
/// </summary>
public static class ExportTabellen
{
    public const string OhneKategorie = "Ohne Kategorie";
    public const string OhneEuerZeile = "—";

    private static readonly NumberFormatInfo Dezimalkomma = new() { NumberDecimalSeparator = "," };

    public static ExportTabelle Belegliste(ExportAuswahlErgebnis auswahl)
    {
        var saetze = auswahl.Belege.SelectMany(b => b.Steuerzeilen).Select(s => s.Satz).Distinct()
            .OrderByDescending(s => s).ToList();

        List<ExportSpalte> spalten =
        [
            new("Rechnungsdatum", SpaltenArt.Datum),
            new("Korrespondent"),
            new("Rechnungsnummer"),
            new("Titel"),
            new("Belegart"),
            new("Kategorie"),
            new("EÜR-Zeile"),
            new("Währung"),
        ];
        foreach (var satz in saetze)
        {
            spalten.Add(new($"Netto {Satz(satz)} %", SpaltenArt.Betrag));
            spalten.Add(new($"USt {Satz(satz)} %", SpaltenArt.Betrag));
        }

        spalten.AddRange(
        [
            new("Netto gesamt", SpaltenArt.Betrag),
            new("USt gesamt", SpaltenArt.Betrag),
            new("Brutto", SpaltenArt.Betrag),
            new("Fälligkeit", SpaltenArt.Datum),
            new("Tags"),
            new("Quelle"),
            new("Ablagepfad"),
            new("SHA-256"),
        ]);

        var zeilen = auswahl.Belege.Select(b =>
        {
            var original = b.Dateien.Where(d => d.Rolle == DateiRolle.Original).OrderBy(d => d.AbgelegtAm).FirstOrDefault();
            List<object?> zeile =
            [
                b.Rechnungsdatum, b.Korrespondent?.Name, b.Rechnungsnummer, b.Titel, b.Belegart.ToString(),
                b.Kategorie?.VollerName, b.Kategorie?.WirksameEuerZeile, b.Waehrung,
            ];
            foreach (var satz in saetze)
            {
                var steuerzeilen = b.Steuerzeilen.Where(s => s.Satz == satz).ToList();
                zeile.Add(steuerzeilen.Count == 0 ? null : steuerzeilen.Sum(s => s.Netto));
                zeile.Add(steuerzeilen.Count == 0 ? null : steuerzeilen.Sum(s => s.Steuer));
            }

            zeile.AddRange(
            [
                b.Netto, b.Steuer, b.Brutto, b.Faelligkeitsdatum,
                string.Join(", ", b.Tags.Select(t => t.Tag?.Name).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)),
                b.Quelle.ToString(), original?.Pfad, original?.Sha256,
            ]);
            return zeile.ToArray();
        }).ToList();

        var euro = auswahl.InEuro.ToList();
        List<object?> summe = ["Summe (EUR)", null, null, null, null, null, null, ExportAuswahlErgebnis.Euro];
        foreach (var satz in saetze)
        {
            var steuerzeilen = euro.SelectMany(b => b.Steuerzeilen).Where(s => s.Satz == satz).ToList();
            summe.Add(steuerzeilen.Sum(s => s.Netto));
            summe.Add(steuerzeilen.Sum(s => s.Steuer));
        }

        summe.AddRange([euro.Sum(b => b.Netto ?? 0), euro.Sum(b => b.Steuer ?? 0), euro.Sum(b => b.Brutto ?? 0)]);

        return new ExportTabelle("Belegliste", "belegliste.csv", spalten, zeilen, [.. summe]);
    }

    /// <summary>Summen je Kategorie, alphabetisch; Belege ohne Kategorie als „Ohne Kategorie“ am Ende.</summary>
    public static IReadOnlyList<KategorieSumme> KategorieSummen(ExportAuswahlErgebnis auswahl) =>
        auswahl.InEuro
            .GroupBy(b => b.KategorieId)
            .Select(g =>
            {
                var kategorie = g.First().Kategorie;
                return new KategorieSumme(
                    kategorie?.VollerName ?? OhneKategorie,
                    kategorie?.WirksameEuerZeile,
                    g.Count(),
                    g.Sum(b => b.Netto ?? 0),
                    g.Sum(b => b.Steuer ?? 0),
                    g.Sum(b => b.Brutto ?? 0));
            })
            .OrderBy(k => k.Kategorie == OhneKategorie && k.Anzahl > 0 ? 1 : 0)
            .ThenBy(k => k.Kategorie, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static ExportTabelle SummenJeKategorie(ExportAuswahlErgebnis auswahl)
    {
        var summen = KategorieSummen(auswahl);
        ExportSpalte[] spalten =
        [
            new("Kategorie"),
            new("EÜR-Zeile"),
            new("Anzahl", SpaltenArt.Anzahl),
            new("Netto", SpaltenArt.Betrag),
            new("USt", SpaltenArt.Betrag),
            new("Brutto", SpaltenArt.Betrag),
        ];
        var zeilen = summen
            .Select(k => new object?[] { k.Kategorie, k.EuerZeile ?? OhneEuerZeile, k.Anzahl, k.Netto, k.Steuer, k.Brutto })
            .ToList();
        object?[] gesamt =
        [
            "Gesamt", null, summen.Sum(k => k.Anzahl), summen.Sum(k => k.Netto), summen.Sum(k => k.Steuer),
            summen.Sum(k => k.Brutto),
        ];

        return new ExportTabelle("Summen je Kategorie", "summen-je-kategorie.csv", spalten, zeilen, gesamt, SummenzeileInCsv: true);
    }

    /// <summary>Netto und Vorsteuer je UStVA-Zeitraum und Steuersatz, mit einer Summenzeile je Zeitraum.</summary>
    public static ExportTabelle Vorsteuer(ExportAuswahlErgebnis auswahl, DateOnly von, DateOnly bis, UstvaZeitraum art)
    {
        ExportSpalte[] spalten =
        [
            new("Zeitraum"),
            new("Steuersatz"),
            new("Netto", SpaltenArt.Betrag),
            new("Vorsteuer", SpaltenArt.Betrag),
        ];
        var euro = auswahl.InEuro.ToList();
        var zeilen = new List<object?[]>();
        foreach (var zeitraum in UstvaZeitraumRechner.Zeitraeume(von, bis, art))
        {
            var steuerzeilen = euro
                .Where(b => b.Rechnungsdatum >= zeitraum.Von && b.Rechnungsdatum <= zeitraum.Bis)
                .SelectMany(b => b.Steuerzeilen)
                .ToList();
            zeilen.AddRange(steuerzeilen
                .GroupBy(s => s.Satz)
                .OrderByDescending(g => g.Key)
                .Select(g => new object?[] { zeitraum.Bezeichnung, $"{Satz(g.Key)} %", g.Sum(s => s.Netto), g.Sum(s => s.Steuer) }));
            zeilen.Add([zeitraum.Bezeichnung, "Summe", steuerzeilen.Sum(s => s.Netto), steuerzeilen.Sum(s => s.Steuer)]);
        }

        var alle = euro.SelectMany(b => b.Steuerzeilen).ToList();
        object?[] gesamt = ["Gesamt", null, alle.Sum(s => s.Netto), alle.Sum(s => s.Steuer)];

        return new ExportTabelle("Vorsteuer", "vorsteuer.csv", spalten, zeilen, gesamt, SummenzeileInCsv: true);
    }

    /// <summary>Summe aller Steuerbeträge der Euro-Belege.</summary>
    public static decimal VorsteuerSumme(ExportAuswahlErgebnis auswahl) =>
        auswahl.InEuro.SelectMany(b => b.Steuerzeilen).Sum(s => s.Steuer);

    /// <summary>Steuersatz ohne überflüssige Nachkommastellen: „19“, „7“, „5,5“.</summary>
    private static string Satz(decimal satz) => satz.ToString("0.##", Dezimalkomma);
}
