using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class Paragraph14PruefungTests
{
    private static readonly ExtrahiertePartei Empfaenger = new("Biber GmbH", "Weg 1", "10115", "Berlin", "DE", null, null, [], null, null);

    private readonly Paragraph14Pruefung _pruefung = new();

    [Fact]
    public void Vollstaendige_Rechnung_wie_im_Design()
    {
        var eingabe = PruefDaten.Eingabe() with { Empfaenger = Empfaenger };

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.Ok("USt-IdNr., Leistungsdatum, Empfänger, Steuersatz vorhanden"));
        _pruefung.Name.ShouldBe("Angaben nach § 14 UStG");
    }

    [Fact]
    public void Fehlendes_Leistungsdatum()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Leistungsdatum = null;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Fehler("Leistungsdatum fehlt"));
    }

    [Fact]
    public void Leistungszeitraum_und_Steuernummer_genuegen()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Leistungsdatum = null;
        beleg.LeistungVon = new DateOnly(2026, 10, 1);
        beleg.LeistungBis = new DateOnly(2026, 10, 31);
        beleg.Korrespondent!.UstIdNr = null;
        beleg.Korrespondent.Steuernummer = "12/345/67890";

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Ok("Steuernummer, Leistungszeitraum, Steuersatz vorhanden"));
    }

    [Fact]
    public void Empfaenger_wird_nur_geprueft_wenn_er_vorliegt()
    {
        var ohneNamen = Empfaenger with { Name = null };

        _pruefung.Pruefen(PruefDaten.Eingabe() with { Empfaenger = ohneNamen }).ShouldBe(PruefBefundWert.Fehler("Empfänger fehlt"));
        _pruefung.Pruefen(PruefDaten.Eingabe()).Ergebnis.ShouldBe(PruefErgebnis.Ok);
    }

    [Fact]
    public void Volle_Pruefung_nennt_alle_fehlenden_Angaben()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Korrespondent!.UstIdNr = null;
        beleg.Korrespondent.Anschrift.Strasse = null;
        beleg.Rechnungsnummer = null;
        beleg.Leistungsdatum = null;
        beleg.Steuerzeilen = [];

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Fehler(
            "Anschrift des Ausstellers, USt-IdNr. oder Steuernummer, Rechnungsnummer, Leistungsdatum und Aufschlüsselung nach Steuersätzen fehlen"));
    }

    [Fact]
    public void Kleinbetrag_genau_an_der_Grenze_braucht_weder_Leistungsdatum_noch_Steuernummer()
    {
        var beleg = KleinbetragOhneZusatzangaben(250.00m);

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg))
            .ShouldBe(PruefBefundWert.Ok("Kleinbetragsrechnung: Aussteller, Datum, Gesamtbetrag, Steuersatz vorhanden"));
    }

    [Fact]
    public void Einen_Cent_ueber_der_Grenze_gilt_die_volle_Pruefung()
    {
        var beleg = KleinbetragOhneZusatzangaben(250.01m);

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Fehler(
            "USt-IdNr. oder Steuernummer, Rechnungsnummer und Leistungsdatum fehlen"));
    }

    [Fact]
    public void Kleinbetrag_ohne_Steuersatz_und_Aussteller()
    {
        var beleg = KleinbetragOhneZusatzangaben(19.99m);
        beleg.KorrespondentId = null;
        beleg.Korrespondent = null;
        beleg.Steuerzeilen = [];
        beleg.Positionen = [PruefDaten.Position(1, 19.99m, steuersatz: null)];

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Fehler(
            "Name des Ausstellers, Anschrift des Ausstellers und Steuersatz fehlen"));
    }

    [Fact]
    public void Kleinbetragsgrenze_kommt_aus_den_Einstellungen()
    {
        var beleg = KleinbetragOhneZusatzangaben(250.01m);

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg, new PruefEinstellungen { KleinbetragGrenze = 300m })).Ergebnis.ShouldBe(PruefErgebnis.Ok);
    }

    [Fact]
    public void Kleinunternehmer_nicht_anwendbar()
    {
        var beleg = KleinbetragOhneZusatzangaben(1000m);

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg, ust: new UstEinstellungen { Status = UstStatus.Kleinunternehmer }))
            .Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }

    [Fact]
    public void Unbekannter_Ust_Status_prueft_voll()
    {
        var beleg = KleinbetragOhneZusatzangaben(1000m);

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg, ust: new UstEinstellungen { Status = UstStatus.Unbekannt }))
            .Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen);
    }

    private static Domain.Entitaeten.Beleg KleinbetragOhneZusatzangaben(decimal brutto)
    {
        var beleg = PruefDaten.Beleg();
        beleg.Brutto = brutto;
        beleg.Rechnungsnummer = null;
        beleg.Leistungsdatum = null;
        beleg.Korrespondent!.UstIdNr = null;
        return beleg;
    }
}
