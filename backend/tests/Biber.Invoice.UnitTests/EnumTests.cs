using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.UnitTests;

/// <summary>
/// Schutz vor Umbenennung: Die Enum-Namen und -Werte sind Teil der Verträge (Datenbank, API, Frontend).
/// Die Typen werden per Namen gesucht, damit auch ein Umbenennen per Refactoring auffällt.
/// </summary>
public sealed class EnumTests
{
    public static TheoryData<string, string[]> Erwartet => new()
    {
        { "Richtung", ["Eingang", "Ausgang"] },
        { "Belegart", ["Rechnung", "Gutschrift", "Quittung"] },
        { "BelegStatus", ["Importiert", "InVerarbeitung", "Fertig", "Fehler"] },
        { "ImportQuelle", ["Upload", "Ordner", "Mail", "Mcp"] },
        { "ImportStatus", ["Angenommen", "InVerarbeitung", "Wartet", "Pausiert", "Fertig", "Dublette", "OhneBeleg", "Abgelehnt", "Fehler"] },
        { "ExtraktionsQuelle", ["XRechnung", "ZUGFeRD", "DocumentIntelligence", "Manuell"] },
        { "ErkanntesFormat", ["Unbekannt", "XRechnungXml", "ZugferdPdf", "Pdf", "Bild"] },
        { "DateiRolle", ["Original", "EingebettetesXml", "Visualisierung", "Vorschau"] },
        { "LogoQuelle", ["Keine", "Upload", "Pdf", "Favicon"] },
        { "MatchingAlgorithmus", ["Keiner", "EinWort", "AlleWoerter", "Exakt", "Regex", "Unscharf", "Auto"] },
        { "PruefungArt", ["Positionssumme", "NettoSteuerBrutto", "Pflichtfelder", "Datum", "Paragraph14", "Dublette", "NeuerKorrespondent", "Konfidenz", "Kosit", "Bankverbindung"] },
        { "PruefErgebnis", ["Ok", "Fehlgeschlagen", "NichtAnwendbar"] },
        { "KorrespondentErkennung", ["Keine", "UstIdNr", "Iban", "Name", "Manuell", "Auto"] },
        { "UstStatus", ["Unbekannt", "Regelbesteuerung", "Kleinunternehmer"] },
        { "UstvaZeitraum", ["Monatlich", "Quartalsweise", "Jaehrlich"] },
        { "Rolle", ["Admin", "Benutzer", "NurLesen"] },
        { "Zahlstatus", ["Offen", "Bezahlt", "Ueberfaellig"] },
        { "Schweregrad", ["Info", "Ok", "Warnung", "Fehler"] },
        { "BenachrichtigungArt", ["DiLimitWarnung", "DiLimitErreicht", "ImportFehler", "KositFehler", "Integritaetsfehler", "IntegritaetOk", "KontoWartetAufFreigabe", "Sonstiges"] },
    };

    [Theory]
    [MemberData(nameof(Erwartet))]
    public void Enum_hat_genau_diese_Namen_in_dieser_Reihenfolge(string enumName, string[] namen)
    {
        var typ = typeof(Rolle).Assembly.GetType($"Biber.Invoice.Domain.Enums.{enumName}");

        typ.ShouldNotBeNull($"Enum {enumName} fehlt");
        typ.IsEnum.ShouldBeTrue();
        Enum.GetNames(typ).ShouldBe(namen);
        Enum.GetValues(typ).Cast<int>().ShouldBe(Enumerable.Range(0, namen.Length));
    }
}
