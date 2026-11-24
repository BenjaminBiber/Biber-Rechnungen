namespace Biber.Invoice.Infrastructure.Module.Korrespondenten;

/// <summary>Ungültige Eingabe; <see cref="Fehler"/> wird als <c>errors</c> der 400-Antwort geliefert.</summary>
public sealed class KorrespondentValidierungsAusnahme(IDictionary<string, string[]> fehler)
    : Exception("Ungültige Eingabe für Korrespondenten.")
{
    public IDictionary<string, string[]> Fehler { get; } = fehler;

    public static KorrespondentValidierungsAusnahme Fuer(string feld, string meldung) =>
        new(new Dictionary<string, string[]> { [feld] = [meldung] });
}

/// <summary>Fachlicher Konflikt (409), z. B. doppelte USt-IdNr.; <see cref="VorhandenId"/> ist der Korrespondent, mit dem er besteht.</summary>
public sealed class KorrespondentKonfliktAusnahme(string meldung, Guid vorhandenId) : Exception(meldung)
{
    public Guid VorhandenId { get; } = vorhandenId;
}
