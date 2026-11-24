namespace Biber.Invoice.Infrastructure.Module.Aufteilen;

/// <summary>Ungültige Seitenaufteilung (400).</summary>
public sealed class AufteilenValidierungsAusnahme(IDictionary<string, string[]> fehler)
    : Exception("Ungültige Aufteilung.")
{
    public IDictionary<string, string[]> Fehler { get; } = fehler;

    public static AufteilenValidierungsAusnahme Fuer(string feld, string meldung) =>
        new(new Dictionary<string, string[]> { [feld] = [meldung] });
}

/// <summary>Der Beleg lässt sich in seinem Zustand nicht aufteilen (409).</summary>
public sealed class AufteilenKonfliktAusnahme(string meldung) : Exception(meldung);
