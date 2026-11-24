namespace Biber.Invoice.Infrastructure.Module.Belege.Schreiben;

/// <summary>Ungültige Änderung an einem Beleg (400).</summary>
public sealed class BelegValidierungsAusnahme(IDictionary<string, string[]> fehler)
    : Exception("Ungültige Änderung am Beleg.")
{
    public IDictionary<string, string[]> Fehler { get; } = fehler;

    public static BelegValidierungsAusnahme Fuer(string feld, string meldung) =>
        new(new Dictionary<string, string[]> { [feld] = [meldung] });
}

/// <summary>Die Änderung widerspricht dem Zustand des Belegs (409).</summary>
public sealed class BelegKonfliktAusnahme(string meldung) : Exception(meldung);
