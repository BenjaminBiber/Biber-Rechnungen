using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class BankverbindungPruefungTests
{
    private const string Bekannt = "DE89370400440532013000";
    private const string Andere = "DE02120300000000202051";

    private readonly BankverbindungPruefung _pruefung = new();

    [Fact]
    public void Bekannte_IBAN_ist_ok()
    {
        var eingabe = Eingabe(korrespondent: [new Bankverbindung(Bekannt), new Bankverbindung(Andere)], beleg: [new Bankverbindung("de89 3704 0044 0532 0130 00")]);

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.Ok("IBAN ist beim Korrespondenten hinterlegt"));
        _pruefung.Name.ShouldBe("Bankverbindung bekannt");
    }

    [Fact]
    public void Abweichende_IBAN_schlaegt_fehl_und_nennt_beide()
    {
        var eingabe = Eingabe(korrespondent: [new Bankverbindung(Bekannt)], beleg: [new Bankverbindung(Andere)]);

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.Fehler(
            "Abweichende IBAN DE02 1203 0000 0000 2020 51 – bisher bekannt: DE89 3704 0044 0532 0130 00"));
    }

    [Fact]
    public void Eine_bekannte_und_eine_abweichende_IBAN_schlaegt_fehl()
    {
        var eingabe = Eingabe(korrespondent: [new Bankverbindung(Bekannt)], beleg: [new Bankverbindung(Bekannt), new Bankverbindung(Andere)]);

        _pruefung.Pruefen(eingabe).Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen);
    }

    [Fact]
    public void Abweichende_IBAN_mit_falscher_Pruefziffer_wird_als_Lesefehler_gemeldet()
    {
        var eingabe = Eingabe(korrespondent: [new Bankverbindung(Bekannt)], beleg: [new Bankverbindung("DE89370400440532013001")]);

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.Fehler(
            "IBAN DE89 3704 0044 0532 0130 01 hat keine gültige Prüfziffer (vermutlich falsch gelesen) – bisher bekannt: DE89 3704 0044 0532 0130 00"));
    }

    [Fact]
    public void Ohne_IBAN_auf_dem_Beleg_nicht_anwendbar()
    {
        _pruefung.Pruefen(Eingabe(korrespondent: [new Bankverbindung(Bekannt)], beleg: []))
            .ShouldBe(PruefBefundWert.NichtAnwendbar("Keine IBAN auf dem Beleg erkannt"));
    }

    [Fact]
    public void Ohne_hinterlegte_IBAN_nicht_anwendbar()
    {
        _pruefung.Pruefen(Eingabe(korrespondent: [], beleg: [new Bankverbindung(Andere)]))
            .ShouldBe(PruefBefundWert.NichtAnwendbar("Beim Korrespondenten ist noch keine IBAN hinterlegt"));
    }

    [Fact]
    public void Ohne_Korrespondent_nicht_anwendbar()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Korrespondent = null;
        beleg.KorrespondentId = null;
        beleg.Bankverbindungen = [new Bankverbindung(Andere)];

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.NichtAnwendbar("Kein Korrespondent zugeordnet"));
    }

    [Fact]
    public void Ausgangsbeleg_nicht_anwendbar()
    {
        var eingabe = Eingabe(korrespondent: [new Bankverbindung(Bekannt)], beleg: [new Bankverbindung(Andere)]);
        eingabe.Beleg.Richtung = Richtung.Ausgang;

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.NichtAnwendbar("Nur für Eingangsbelege"));
    }

    private static PruefEingabe Eingabe(List<Bankverbindung> korrespondent, List<Bankverbindung> beleg)
    {
        var b = PruefDaten.Beleg();
        b.Korrespondent!.Bankverbindungen = korrespondent;
        b.Bankverbindungen = beleg;
        return PruefDaten.Eingabe(b);
    }
}
