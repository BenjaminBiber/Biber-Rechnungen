using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Infrastructure.Module.Belegpruefung;

/// <summary>Gemeinsame Regeln der beiden Belegprüfungs-Schritte.</summary>
internal static class BelegpruefungRegeln
{
    public const string GrundOhneDaten = "weder Betrag noch Rechnungsnummer oder Aussteller erkannt";

    /// <summary>
    /// Geprüft werden nur Dateien, die ungefragt kommen (Mail, Ordner) – nicht Upload und MCP, nicht „Erneut auslesen“,
    /// keine Teile eines aufgeteilten Belegs und keine Datei, die ein Benutzer schon als Beleg übernommen hat.
    /// </summary>
    public static bool Gilt(VerarbeitungsKontext kontext) =>
        !kontext.IstWiederholung
        && kontext.Vorgang.Quelle is ImportQuelle.Mail or ImportQuelle.Ordner
        && !kontext.Vorgang.AlsBelegBestaetigt
        && kontext.Beleg.AufgeteiltAusBelegId is null;

    public static SchrittErgebnis Aussortieren(string grund) =>
        SchrittErgebnis.Beendet(ImportStatus.Aussortiert, $"Kein Beleg: {grund}");
}
