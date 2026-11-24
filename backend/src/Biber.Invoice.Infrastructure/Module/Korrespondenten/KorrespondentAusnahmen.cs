namespace Biber.Invoice.Infrastructure.Module.Korrespondenten;

/// <summary>Ungültige Eingabe; <see cref="Fehler"/> wird als <c>errors</c> der 400-Antwort geliefert.</summary>
public sealed class KorrespondentValidierungsAusnahme(IDictionary<string, string[]> fehler)
    : Exception("Ungültige Eingabe für Korrespondenten.")
{
    public IDictionary<string, string[]> Fehler { get; } = fehler;

    public static KorrespondentValidierungsAusnahme Fuer(string feld, string meldung) =>
        new(new Dictionary<string, string[]> { [feld] = [meldung] });
}

/// <summary>Fachlicher Konflikt (409), z. B. doppelte USt-IdNr.</summary>
public sealed class KorrespondentKonfliktAusnahme(string meldung) : Exception(meldung);
