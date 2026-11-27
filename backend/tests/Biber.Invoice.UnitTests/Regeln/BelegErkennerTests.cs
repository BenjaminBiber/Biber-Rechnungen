using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class BelegErkennerTests
{
    private const string Rechnung =
        "Muster GmbH · Hauptstraße 1 · 12345 Berlin\nRechnung\nRechnungsnummer: RE-2026-17\nRechnungsdatum: 07.10.2026\n"
        + "Pos. Beschreibung Menge Preis\n1 Beratung 2 Std. 180,00 €\nNetto 180,00 € zzgl. 19 % MwSt. 34,20 €\nGesamt 214,20 €\n"
        + "Es gelten unsere Allgemeinen Geschäftsbedingungen. Datenschutz: www.muster.de/datenschutz";

    private const string Agb =
        "Allgemeine Geschäftsbedingungen der Muster GmbH\n§ 1 Geltungsbereich\nDiese AGB gelten für alle Bestellungen.\n"
        + "§ 5 Zahlung\nDie Rechnung ist innerhalb von 14 Tagen ab Rechnungsdatum zahlbar. Bitte geben Sie die Rechnungsnummer an.\n"
        + "§ 6 Aufrechnung\nEine Aufrechnung ist nur mit unbestrittenen Forderungen zulässig. Alle Preise inkl. MwSt., Versand 4,90 €.";

    [Theory]
    [InlineData("AGB.pdf", "AGB")]
    [InlineData("agb_2026.pdf", "AGB")]
    [InlineData("Allgemeine-Geschaeftsbedingungen.pdf", "AGB")]
    [InlineData("Muster-Widerrufsformular.pdf", "Widerrufsbelehrung")]
    [InlineData("Datenschutzhinweise.pdf", "Datenschutzhinweise")]
    [InlineData("Lieferschein 4711.pdf", "Lieferschein")]
    [InlineData("Auftragsbestätigung_123.pdf", "Auftragsbestätigung")]
    [InlineData("Angebot-2026-03.pdf", "Angebot")]
    [InlineData("Newsletter_Oktober.pdf", "Werbung")]
    [InlineData("Sicherheitsdatenblatt.pdf", "Produktinformation")]
    public void Dateiname_der_etwas_anderes_nennt_ist_kein_Beleg(string dateiname, string art)
    {
        var einschaetzung = BelegErkenner.Einschaetzen(dateiname, null);

        einschaetzung.Urteil.ShouldBe(BelegUrteil.KeinBeleg);
        einschaetzung.Grund.ShouldBe($"Dateiname deutet auf {art}");
    }

    [Theory]
    [InlineData("Rechnung_123.pdf")]
    [InlineData("Rechnung123.pdf")]
    [InlineData("MeineRechnung.pdf")]
    [InlineData("Invoice-2026-17.pdf")]
    [InlineData("Gutschrift.pdf")]
    [InlineData("Quittung.jpg")]
    [InlineData("Lieferschein_und_Rechnung.pdf")]
    public void Dateiname_der_einen_Beleg_nennt_gewinnt_auch_gegen_AGB_Text(string dateiname)
    {
        BelegErkenner.Einschaetzen(dateiname, Agb).Urteil.ShouldBe(BelegUrteil.Beleg);
    }

    [Fact]
    public void Berechnung_im_Dateinamen_ist_kein_Hinweis_auf_eine_Rechnung()
    {
        BelegErkenner.Einschaetzen("Berechnung.pdf", null).Urteil.ShouldBe(BelegUrteil.Unklar);
    }

    [Theory]
    [InlineData("dokument.pdf")]
    [InlineData("scan_0001.pdf")]
    [InlineData(null)]
    public void Neutraler_Dateiname_ohne_Text_ist_unklar(string? dateiname)
    {
        BelegErkenner.Einschaetzen(dateiname, null).ShouldBe(new BelegEinschaetzung(BelegUrteil.Unklar, ""));
    }

    [Fact]
    public void AGB_Text_ist_kein_Beleg_obwohl_er_Rechnung_und_Rechnungsnummer_erwaehnt()
    {
        var einschaetzung = BelegErkenner.Einschaetzen("dokument.pdf", Agb);

        einschaetzung.Urteil.ShouldBe(BelegUrteil.KeinBeleg);
        einschaetzung.Grund.ShouldBe("Inhalt sieht nach AGB aus");
    }

    [Fact]
    public void Rechnung_mit_AGB_und_Datenschutz_in_der_Fusszeile_ist_ein_Beleg()
    {
        BelegErkenner.Einschaetzen("dokument.pdf", Rechnung).Urteil.ShouldBe(BelegUrteil.Beleg);
    }

    [Theory]
    [InlineData("Lieferschein\nLieferschein-Nr. 4711\nArtikel Menge\nSchrauben 100\nRechnung folgt separat.", "Lieferschein")]
    [InlineData("Muster GmbH\nAuftragsbestätigung Nr. 815\nVielen Dank für Ihren Auftrag. Die Rechnung erhalten Sie mit der Lieferung.", "Auftragsbestätigung")]
    [InlineData("Ihr Angebot\nAngebotsnummer A-2026-3\nGültig bis 31.12.2026\nGesamt 1.190,00 € inkl. 19 % MwSt.", "Angebot")]
    [InlineData("Widerrufsbelehrung\nSie haben das Recht, binnen vierzehn Tagen ohne Angabe von Gründen diesen Vertrag zu widerrufen.", "Widerrufsbelehrung")]
    [InlineData("Datenschutzhinweise nach Art. 13 DSGVO\nWir verarbeiten Ihre Daten zur Erstellung der Rechnung.", "Datenschutzhinweise")]
    public void Text_mit_anderer_Ueberschrift_ist_kein_Beleg(string text, string art)
    {
        BelegErkenner.Einschaetzen("anhang.pdf", text).ShouldBe(new BelegEinschaetzung(BelegUrteil.KeinBeleg, $"Inhalt sieht nach {art} aus"));
    }

    [Theory]
    [InlineData("Rechnungs-Nr.: 2026-17")]
    [InlineData("Invoice No. INV-0815")]
    [InlineData("Re.-Nr. 4711")]
    [InlineData("Lieferschein-Nr. 12 / Rechnungsnummer RE-2026-0815")]
    public void Rechnungsnummer_mit_Wert_macht_einen_Beleg(string text)
    {
        BelegErkenner.Einschaetzen("anhang.pdf", text).Urteil.ShouldBe(BelegUrteil.Beleg);
    }

    [Theory]
    [InlineData("Quittung\nBetrag dankend erhalten: 25,00 €")]
    [InlineData("Gutschrift\nWir schreiben Ihnen 49,90 € gut.")]
    [InlineData("Nebenkostenabrechnung 2025")]
    [InlineData("Kassenbon\nSumme EUR 12,90\nMwSt 19 % 2,06")]
    [InlineData("Summe 12,90 €\nenthaltene MwSt. 19 % 2,06 €")]
    public void Belegwort_oder_Steuer_mit_Betrag_macht_einen_Beleg(string text)
    {
        BelegErkenner.Einschaetzen("anhang.pdf", text).Urteil.ShouldBe(BelegUrteil.Beleg);
    }

    [Fact]
    public void Begriff_weit_hinten_zaehlt_nicht_als_Ueberschrift()
    {
        var text = new string('x', BelegErkenner.Titelbereich) + " Lieferschein";

        BelegErkenner.Einschaetzen("anhang.pdf", text).Urteil.ShouldBe(BelegUrteil.Unklar);
    }

    [Fact]
    public void Text_ohne_Hinweise_ist_unklar()
    {
        BelegErkenner.Einschaetzen("anhang.pdf", "Herzliche Grüße aus dem Urlaub!").Urteil.ShouldBe(BelegUrteil.Unklar);
    }
}
