using System.Globalization;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>Deutsche Schreibweise für die Detailtexte, unabhängig von der Kultur des Servers.</summary>
internal static class PruefFormat
{
    private static readonly NumberFormatInfo Deutsch = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    /// <summary>„1.234,50 €“.</summary>
    public static string Euro(decimal betrag) => betrag.ToString("#,##0.00", Deutsch) + " €";

    /// <summary>„19 %“ bzw. „5,5 %“.</summary>
    public static string Satz(decimal satz) => satz.ToString("0.##", Deutsch) + " %";

    /// <summary>„0,74“.</summary>
    public static string Zahl(decimal wert) => wert.ToString("0.00", Deutsch);

    /// <summary>„19.11.2026“.</summary>
    public static string Datum(DateOnly datum) => datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>„DE89 3704 0044 0532 0130 00“.</summary>
    public static string Iban(string iban) => string.Join(' ', iban.Chunk(4).Select(gruppe => new string(gruppe)));

    /// <summary>„A“, „A und B“, „A, B und C“.</summary>
    public static string Aufzaehlung(IReadOnlyList<string> teile) => teile.Count switch
    {
        0 => "",
        1 => teile[0],
        _ => string.Join(", ", teile.Take(teile.Count - 1)) + " und " + teile[^1],
    };

    /// <summary>„Rechnungsnummer fehlt“ bzw. „Datum und Rechnungsnummer fehlen“.</summary>
    public static string Fehlt(IReadOnlyList<string> teile) => Aufzaehlung(teile) + (teile.Count == 1 ? " fehlt" : " fehlen");
}
