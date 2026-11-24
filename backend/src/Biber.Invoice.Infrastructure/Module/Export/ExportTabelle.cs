namespace Biber.Invoice.Infrastructure.Module.Export;

/// <summary>Art einer Spalte; bestimmt das Format in CSV und Excel.</summary>
public enum SpaltenArt
{
    Text,
    Datum,
    Betrag,
    Anzahl,
}

public sealed record ExportSpalte(string Name, SpaltenArt Art = SpaltenArt.Text);

/// <summary>
/// Eine Exporttabelle unabhängig vom Format. Zellen sind <c>string</c>, <see cref="DateOnly"/>, <c>decimal</c>,
/// <c>int</c> oder <c>null</c>. <see cref="Summenzeile"/> steht in Excel fett unter den Zeilen, in CSV nur mit
/// <see cref="SummenzeileInCsv"/>.
/// </summary>
/// <param name="Blatt">Name des Excel-Blatts.</param>
/// <param name="Datei">Dateiname der CSV-Datei.</param>
public sealed record ExportTabelle(
    string Blatt,
    string Datei,
    IReadOnlyList<ExportSpalte> Spalten,
    IReadOnlyList<object?[]> Zeilen,
    object?[]? Summenzeile = null,
    bool SummenzeileInCsv = false);
