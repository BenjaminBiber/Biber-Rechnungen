using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Biber.Invoice.Infrastructure.Module.Export;

/// <summary>Schreibt <see cref="ExportTabelle"/>n als CSV (Semikolon, UTF-8 mit BOM, Dezimalkomma) oder als Excel-Mappe.</summary>
public static class TabellenSchreiber
{
    public const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Dezimalkomma ohne Tausendertrennzeichen, unabhängig von den Kulturdaten des Containers.</summary>
    private static readonly NumberFormatInfo Dezimalkomma = new() { NumberDecimalSeparator = ",", NegativeSign = "-" };

    public static void CsvSchreiben(ExportTabelle tabelle, Stream ziel)
    {
        using var schreiber = new StreamWriter(ziel, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        schreiber.NewLine = "\r\n";
        schreiber.WriteLine(Zeile(tabelle.Spalten.Select(s => s.Name)));
        var zeilen = tabelle.SummenzeileInCsv && tabelle.Summenzeile is { } summe
            ? tabelle.Zeilen.Append(summe)
            : tabelle.Zeilen;
        foreach (var zeile in zeilen)
        {
            schreiber.WriteLine(Zeile(zeile.Select(CsvWert)));
        }
    }

    public static void ExcelSchreiben(IEnumerable<ExportTabelle> tabellen, Stream ziel)
    {
        using var mappe = new XLWorkbook();
        foreach (var tabelle in tabellen)
        {
            BlattSchreiben(mappe.Worksheets.Add(tabelle.Blatt), tabelle);
        }

        mappe.SaveAs(ziel);
    }

    private static void BlattSchreiben(IXLWorksheet blatt, ExportTabelle tabelle)
    {
        for (var s = 0; s < tabelle.Spalten.Count; s++)
        {
            blatt.Cell(1, s + 1).Value = tabelle.Spalten[s].Name;
        }

        blatt.Row(1).Style.Font.Bold = true;
        blatt.SheetView.FreezeRows(1);

        var r = 2;
        foreach (var zeile in tabelle.Zeilen)
        {
            ZeileSchreiben(blatt, r++, tabelle.Spalten, zeile);
        }

        if (tabelle.Summenzeile is { } summe)
        {
            ZeileSchreiben(blatt, r, tabelle.Spalten, summe);
            blatt.Row(r).Style.Font.Bold = true;
        }

        // Breite aus der Textlänge statt AdjustToContents: Das misst mit Schriftdaten, die im Container fehlen können.
        for (var s = 0; s < tabelle.Spalten.Count; s++)
        {
            var laengste = tabelle.Zeilen.Select(z => s < z.Length ? CsvWert(z[s]).Length : 0)
                .Append(tabelle.Spalten[s].Name.Length)
                .Max();
            blatt.Column(s + 1).Width = Math.Clamp(laengste + 2, 8, 60);
        }
    }

    private static void ZeileSchreiben(IXLWorksheet blatt, int r, IReadOnlyList<ExportSpalte> spalten, object?[] werte)
    {
        for (var s = 0; s < spalten.Count && s < werte.Length; s++)
        {
            var zelle = blatt.Cell(r, s + 1);
            switch (werte[s])
            {
                case null:
                    break;
                case DateOnly datum:
                    zelle.Value = datum.ToDateTime(TimeOnly.MinValue);
                    zelle.Style.DateFormat.Format = "dd.mm.yyyy";
                    break;
                case decimal betrag:
                    zelle.Value = betrag;
                    zelle.Style.NumberFormat.Format = "#,##0.00";
                    break;
                case int anzahl:
                    zelle.Value = anzahl;
                    zelle.Style.NumberFormat.Format = "0";
                    break;
                case var wert:
                    zelle.Value = Convert.ToString(wert, CultureInfo.InvariantCulture);
                    break;
            }
        }
    }

    private static string CsvWert(object? wert) => wert switch
    {
        null => "",
        DateOnly datum => datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        decimal betrag => betrag.ToString("0.00", Dezimalkomma),
        int anzahl => anzahl.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(wert, CultureInfo.InvariantCulture) ?? "",
    };

    private static string Zeile(IEnumerable<string> felder) => string.Join(';', felder.Select(Maskieren));

    private static string Maskieren(string feld) =>
        feld.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{feld.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : feld;
}
