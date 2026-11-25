using System.Text;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.ERechnung;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class ERechnungParserTests
{
    private const string Iban = "DE02120300000000202051";

    private readonly ERechnungParser _parser = new();

    private ExtraktionsErgebnis Lesen(string datei) =>
        _parser.Parsen(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Testdateien", "erechnung", datei)));

    [Fact]
    public void XRechnung_CII_liefert_alle_Felder()
    {
        var e = Lesen("xrechnung-cii.xml");

        e.Quelle.ShouldBe(ExtraktionsQuelle.XRechnung);
        e.Belegart.ShouldBe(Belegart.Rechnung);
        e.Rechnungsnummer.ShouldBe("RE-2026-0001");
        e.Rechnungsdatum.ShouldBe(new DateOnly(2026, 3, 15));
        e.Leistungsdatum.ShouldBe(new DateOnly(2026, 3, 10));
        e.LeistungVon.ShouldBeNull();
        e.LeistungBis.ShouldBeNull();
        e.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 3, 29));
        e.Waehrung.ShouldBe("EUR");
        e.Netto.ShouldBe(155.00m);
        e.Steuer.ShouldBe(29.45m);
        e.Brutto.ShouldBe(184.45m);
        e.Konfidenz.ShouldBeNull();
        e.Seiten.ShouldBe(1);

        e.Steuerzeilen.ShouldBe([new ExtrahierteSteuerzeile(19m, 155.00m, 29.45m)]);
        e.Positionen.ShouldBe(
        [
            new ExtrahiertePosition(1, "Kopierpapier A4 – 80 g/m², 500 Blatt", 10m, "Stück", 4.50m, 45.00m, 19m),
            new ExtrahiertePosition(2, "Montage", 2m, "Std.", 55.00m, 110.00m, 19m),
        ]);
        PruefeVerkaeufer(e.Aussteller);
        PruefeKaeufer(e.Empfaenger);

        e.Volltext.ShouldBe(string.Join('\n',
            "RE-2026-0001",
            "Muster Bürobedarf GmbH", "Lieferweg 1", "10115", "Berlin", "DE123456789", "30/123/45678",
            "rechnung@muster-buero.de",
            "Biber Handwerk e.K.", "Hauptstraße 5", "80331", "München", "DE987654321", "einkauf@biber-handwerk.de",
            "Kopierpapier A4 – 80 g/m², 500 Blatt", "Montage",
            "Zahlbar innerhalb 14 Tagen ohne Abzug"));
    }

    [Fact]
    public void XRechnung_UBL_Invoice_liefert_alle_Felder()
    {
        var e = Lesen("xrechnung-ubl-invoice.xml");

        e.Quelle.ShouldBe(ExtraktionsQuelle.XRechnung);
        e.Belegart.ShouldBe(Belegart.Rechnung);
        e.Rechnungsnummer.ShouldBe("RE-2026-0002");
        e.Rechnungsdatum.ShouldBe(new DateOnly(2026, 4, 1));
        e.Leistungsdatum.ShouldBe(new DateOnly(2026, 3, 31));
        e.LeistungVon.ShouldBeNull();
        e.LeistungBis.ShouldBeNull();
        e.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 4, 15));
        e.Waehrung.ShouldBe("EUR");
        e.Netto.ShouldBe(100.00m);
        e.Steuer.ShouldBe(19.00m);
        e.Brutto.ShouldBe(119.00m);
        e.Seiten.ShouldBe(1);

        e.Steuerzeilen.ShouldBe([new ExtrahierteSteuerzeile(19m, 100.00m, 19.00m)]);
        e.Positionen.ShouldBe(
        [
            new ExtrahiertePosition(1, "Druckerpatrone schwarz", 3m, "Stück", 30.00m, 90.00m, 19m),
            new ExtrahiertePosition(2, "Reinigungsmittel", 4m, "l", 2.50m, 10.00m, 19m),
        ]);
        PruefeVerkaeufer(e.Aussteller);
        PruefeKaeufer(e.Empfaenger);
        e.Volltext.ShouldNotBeNull().ShouldContain("Druckerpatrone schwarz");
    }

    [Fact]
    public void UBL_CreditNote_wird_als_Gutschrift_erkannt()
    {
        var e = Lesen("ubl-creditnote.xml");

        e.Quelle.ShouldBe(ExtraktionsQuelle.XRechnung);
        e.Belegart.ShouldBe(Belegart.Gutschrift);
        e.Rechnungsnummer.ShouldBe("GS-2026-0003");
        e.Rechnungsdatum.ShouldBe(new DateOnly(2026, 4, 20));
        e.Leistungsdatum.ShouldBeNull();
        e.LeistungVon.ShouldBeNull();
        e.LeistungBis.ShouldBeNull();
        e.Faelligkeitsdatum.ShouldBeNull();
        e.Waehrung.ShouldBe("EUR");
        e.Netto.ShouldBe(30.00m);
        e.Steuer.ShouldBe(5.70m);
        e.Brutto.ShouldBe(35.70m);

        e.Steuerzeilen.ShouldBe([new ExtrahierteSteuerzeile(19m, 30.00m, 5.70m)]);
        e.Positionen.ShouldBe([new ExtrahiertePosition(1, "Gutschrift Druckerpatrone", 1m, "Stück", 30.00m, 30.00m, 19m)]);
        PruefeVerkaeufer(e.Aussteller);
        PruefeKaeufer(e.Empfaenger);
    }

    [Fact]
    public void ZUGFeRD_EN16931_liefert_alle_Felder()
    {
        var e = Lesen("zugferd-en16931.xml");

        // Die Profilkennung des EN-16931-Profils nennt weder ZUGFeRD noch Factur-X; eingebettet setzt der Schritt ZUGFeRD.
        e.Quelle.ShouldBe(ExtraktionsQuelle.XRechnung);
        e.Belegart.ShouldBe(Belegart.Rechnung);
        e.Rechnungsnummer.ShouldBe("ZF-2026-0004");
        e.Rechnungsdatum.ShouldBe(new DateOnly(2026, 5, 5));
        e.Leistungsdatum.ShouldBe(new DateOnly(2026, 5, 2));
        e.LeistungVon.ShouldBeNull();
        e.LeistungBis.ShouldBeNull();
        e.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 6, 4));
        e.Waehrung.ShouldBe("EUR");
        e.Netto.ShouldBe(80.00m);
        e.Steuer.ShouldBe(15.20m);
        e.Brutto.ShouldBe(95.20m);

        e.Steuerzeilen.ShouldBe([new ExtrahierteSteuerzeile(19m, 80.00m, 15.20m)]);
        e.Positionen.ShouldBe(
        [
            new ExtrahiertePosition(1, "Kabel – NYM-J 3x1,5", 50m, "m", 1.20m, 60.00m, 19m),
            new ExtrahiertePosition(2, "Schrauben", 2.5m, "kg", 8.00m, 20.00m, 19m),
        ]);
        PruefeVerkaeufer(e.Aussteller);
        PruefeKaeufer(e.Empfaenger);
        e.Aussteller!.Bankverbindungen.ShouldBe([new Bankverbindung(Iban, "BYLADEM1001")]);
    }

    [Fact]
    public void Textzeilen_mit_Menge_und_Betrag_0_haengen_an_der_vorherigen_Position()
    {
        var e = Lesen("textzeilen.xml");

        // Zeile 2 (Name „ohne“, Beschreibung „incl. Montage“) und Zeile 4 (nur Name) sind reine Textzeilen;
        // der Werbekugelschreiber hat Betrag 0, aber Menge 1, und bleibt eine eigene Position.
        e.Positionen.ShouldBe(
        [
            new ExtrahiertePosition(1, "Kabel – NYM-J 3x1,5 – incl. Montage", 50m, "m", 1.20m, 60.00m, 19m),
            new ExtrahiertePosition(3, "Schrauben – Lieferung frei Baustelle", 2.5m, "kg", 8.00m, 20.00m, 19m),
            new ExtrahiertePosition(5, "Werbekugelschreiber", 1m, "Stück", 0.00m, 0.00m, 19m),
        ]);
        e.Netto.ShouldBe(80.00m);
    }

    [Fact]
    public void Zwei_Steuersaetze_ergeben_zwei_Steuerzeilen()
    {
        var e = Lesen("zwei-steuersaetze.xml");

        e.Rechnungsnummer.ShouldBe("RE-2026-0005");
        e.Rechnungsdatum.ShouldBe(new DateOnly(2026, 6, 10));
        e.Leistungsdatum.ShouldBe(new DateOnly(2026, 6, 9));
        e.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 6, 24));
        e.Netto.ShouldBe(300.00m);
        e.Steuer.ShouldBe(45.00m);
        e.Brutto.ShouldBe(345.00m);

        e.Steuerzeilen.ShouldBe(
        [
            new ExtrahierteSteuerzeile(19m, 200.00m, 38.00m),
            new ExtrahierteSteuerzeile(7m, 100.00m, 7.00m),
        ]);
        e.Positionen.ShouldBe(
        [
            new ExtrahiertePosition(1, "Bürostuhl", 1m, "Stück", 200.00m, 200.00m, 19m),
            new ExtrahiertePosition(2, "Fachbuch Steuerrecht", 2m, "Stück", 50.00m, 100.00m, 7m),
        ]);
        PruefeVerkaeufer(e.Aussteller);
        PruefeKaeufer(e.Empfaenger);
    }

    [Fact]
    public void Leistungszeitraum_wird_gelesen()
    {
        var e = Lesen("leistungszeitraum.xml");

        e.Rechnungsnummer.ShouldBe("RE-2026-0006");
        e.Rechnungsdatum.ShouldBe(new DateOnly(2026, 7, 1));
        e.Leistungsdatum.ShouldBeNull();
        e.LeistungVon.ShouldBe(new DateOnly(2026, 6, 1));
        e.LeistungBis.ShouldBe(new DateOnly(2026, 6, 30));
        e.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 7, 15));
        e.Netto.ShouldBe(89.00m);
        e.Steuer.ShouldBe(16.91m);
        e.Brutto.ShouldBe(105.91m);

        e.Steuerzeilen.ShouldBe([new ExtrahierteSteuerzeile(19m, 89.00m, 16.91m)]);
        e.Positionen.ShouldBe(
            [new ExtrahiertePosition(1, "Wartungsvertrag Kopierer – Juni 2026", 1m, "Monat", 89.00m, 89.00m, 19m)]);
        PruefeVerkaeufer(e.Aussteller);
        PruefeKaeufer(e.Empfaenger);
    }

    [Fact]
    public void FacturX_Profil_ergibt_Quelle_ZUGFeRD_und_unbekannte_Einheit_bleibt_Code()
    {
        var e = Lesen("factur-x-basic.xml");

        e.Quelle.ShouldBe(ExtraktionsQuelle.ZUGFeRD);
        e.Rechnungsnummer.ShouldBe("FX-2026-0007");
        e.Brutto.ShouldBe(428.40m);
        e.Positionen.ShouldBe([new ExtrahiertePosition(1, "Mietgerät Hebebühne", 3m, "DAY", 120.00m, 360.00m, 19m)]);

        // Basic kennt keinen Kontakt und keine Steuernummer: E-Mail kommt aus der elektronischen Adresse.
        var a = e.Aussteller.ShouldNotBeNull();
        a.Email.ShouldBe("rechnung@muster-buero.de");
        a.UstIdNr.ShouldBe("DE123456789");
        a.Steuernummer.ShouldBeNull();
        a.Bankverbindungen.ShouldBe([new Bankverbindung(Iban)]); // ohne BIC in der Datei
        e.Empfaenger.ShouldNotBeNull().Email.ShouldBe("einkauf@biber-handwerk.de");
    }

    [Theory]
    [InlineData("C62", "Stück")]
    [InlineData("H87", "Stück")]
    [InlineData("KGM", "kg")]
    [InlineData("LTR", "l")]
    [InlineData("MTR", "m")]
    [InlineData("HUR", "Std.")]
    [InlineData("MON", "Monat")]
    [InlineData("DAY", "DAY")]
    [InlineData(null, null)]
    public void Einheit_wird_in_Klartext_abgebildet(string? code, string? klartext) =>
        ERechnungParser.EinheitKlartext(code).ShouldBe(klartext);

    [Theory]
    [InlineData("urn:cen.eu:en16931:2017#compliant#urn:xeinkauf.de:kosit:xrechnung_3.0", ExtraktionsQuelle.XRechnung)]
    [InlineData("urn:cen.eu:en16931:2017#conformant#urn:zugferd.de:2p0:extended", ExtraktionsQuelle.ZUGFeRD)]
    [InlineData("urn:cen.eu:en16931:2017#compliant#urn:factur-x.eu:1p0:basic", ExtraktionsQuelle.ZUGFeRD)]
    [InlineData("urn:ferd:CrossIndustryDocument:invoice:1p0:comfort", ExtraktionsQuelle.ZUGFeRD)]
    [InlineData(null, ExtraktionsQuelle.XRechnung)]
    public void Quelle_folgt_der_Profilkennung(string? profil, ExtraktionsQuelle quelle) =>
        ERechnungParser.QuelleAusProfil(profil).ShouldBe(quelle);

    [Theory]
    [InlineData("<rsm:CrossIndustryInvoice xmlns:rsm=\"urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100\">")]
    [InlineData("kein xml")]
    [InlineData("<html><body>Rechnung</body></html>")]
    [InlineData("")]
    public void Kaputtes_XML_wirft_ERechnungFormatException(string inhalt) =>
        Should.Throw<ERechnungFormatException>(() => _parser.Parsen(Encoding.UTF8.GetBytes(inhalt)));

    [Fact]
    public void XML_mit_DTD_wird_abgewiesen()
    {
        const string xml = """
            <?xml version="1.0"?>
            <!DOCTYPE x [<!ENTITY e SYSTEM "file:///c:/windows/win.ini">]>
            <Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2">&e;</Invoice>
            """;

        Should.Throw<ERechnungFormatException>(() => _parser.Parsen(Encoding.UTF8.GetBytes(xml)));
    }

    private static void PruefeVerkaeufer(ExtrahiertePartei? partei)
    {
        var p = partei.ShouldNotBeNull();
        p.Name.ShouldBe("Muster Bürobedarf GmbH");
        p.Strasse.ShouldBe("Lieferweg 1");
        p.Plz.ShouldBe("10115");
        p.Ort.ShouldBe("Berlin");
        p.Land.ShouldBe("DE");
        p.UstIdNr.ShouldBe("DE123456789");
        p.Steuernummer.ShouldBe("30/123/45678");
        p.Bankverbindungen.Select(b => b.Iban).ShouldBe([Iban]);
        p.Email.ShouldBe("rechnung@muster-buero.de");
        p.Website.ShouldBeNull();
    }

    private static void PruefeKaeufer(ExtrahiertePartei? partei)
    {
        var p = partei.ShouldNotBeNull();
        p.Name.ShouldBe("Biber Handwerk e.K.");
        p.Strasse.ShouldBe("Hauptstraße 5");
        p.Plz.ShouldBe("80331");
        p.Ort.ShouldBe("München");
        p.Land.ShouldBe("DE");
        p.UstIdNr.ShouldBe("DE987654321");
        p.Steuernummer.ShouldBeNull();
        p.Bankverbindungen.ShouldBeEmpty();
        p.Email.ShouldBe("einkauf@biber-handwerk.de");
        p.Website.ShouldBeNull();
    }
}
