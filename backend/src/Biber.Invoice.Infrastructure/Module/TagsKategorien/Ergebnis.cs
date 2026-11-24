namespace Biber.Invoice.Infrastructure.Module.TagsKategorien;

/// <summary>Fachlicher Fehler einer Änderung: 400 (mit <see cref="Feld"/>), 404 oder 409.</summary>
public sealed record Fehler(int Status, string Meldung, string? Feld = null)
{
    public static Fehler Ungueltig(string feld, string meldung) => new(400, meldung, feld);
    public static Fehler NichtGefunden() => new(404, "Nicht gefunden.");
    public static Fehler Konflikt(string meldung) => new(409, meldung);
}

/// <summary>Wert oder <see cref="Fehler"/>.</summary>
public sealed record Ergebnis<T>(T? Wert, Fehler? Fehler)
{
    public static implicit operator Ergebnis<T>(T wert) => new(wert, null);
    public static implicit operator Ergebnis<T>(Fehler fehler) => new(default, fehler);
}
