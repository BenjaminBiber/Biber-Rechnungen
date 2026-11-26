using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class SkontoErkennerTests
{
    [Theory]
    [InlineData("Zahlbar innerhalb von 14 Tagen mit 2 % Skonto, 30 Tage netto.", 2, 14)]
    [InlineData("Zahlungsbedingungen: 14 Tage 2% Skonto, 30 Tage rein netto", 2, 14)]
    [InlineData("Bei Zahlung innerhalb von 10 Tagen gewähren wir 3 % Skonto.", 3, 10)]
    [InlineData("2,5 % Skonto bei Zahlung innerhalb 7 Kalendertagen", 2.5, 7)]
    [InlineData("Zahlbar innerhalb 8 Tagen, 2 % Skonto", 2, 8)]
    [InlineData("Zahlbar innerhalb 10 Tagen mit 2 % Skonto oder 30 Tagen ohne Skonto", 2, 10)]
    [InlineData("#SKONTO#TAGE=14#PROZENT=2.00#\n", 2, 14)]
    [InlineData("Netto 100,00 € zzgl. 19 % USt.\n3 Prozent Skonto innerhalb von 5 Tagen", 3, 5)]
    public void Erkennt_Prozent_und_Tage(string text, decimal prozent, int tage)
    {
        SkontoErkenner.AusText(text).ShouldBe(new SkontoAngabe(prozent, tage, null));
    }

    [Theory]
    [InlineData("Bei Zahlung bis 12.12.2026 abzüglich 2 % Skonto, bis 31.12.2026 netto")]
    [InlineData("Skonto: 2 % bis 12.12.26")]
    public void Erkennt_festes_Datum(string text)
    {
        SkontoErkenner.AusText(text).ShouldBe(new SkontoAngabe(2m, null, new DateOnly(2026, 12, 12)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Zahlbar innerhalb von 30 Tagen ohne Abzug.")]
    [InlineData("Skonto wird nicht gewährt. Zahlbar in 14 Tagen.")]
    [InlineData("Kein Skonto, 2 % Verzugszins nach 30 Tagen")]
    [InlineData("Skonto: 19 % innerhalb von 14 Tagen")]
    [InlineData("2 % Skonto")]
    public void Erkennt_kein_Skonto(string? text)
    {
        SkontoErkenner.AusText(text).ShouldBeNull();
    }

    [Theory]
    [InlineData("2,00% Rabatt aus 529,81 €\n-10,59 €", 2)]
    [InlineData("2,00% Rabatt aus 529,81 € -10,59 €", 2)]
    [InlineData("abzüglich 3 % Skonto -15,00 €", 3)]
    [InlineData("Skonto 2 % von 1.250,00 EUR 25,00-", 2)]
    [InlineData("Sondernachlass 5 % auf 200,00 € − 10,00 €", 5)]
    public void Erkennt_bereits_abgezogenes_Skonto_ohne_Frist(string text, decimal prozent)
    {
        SkontoErkenner.AusText(text).ShouldBe(new SkontoAngabe(prozent, null, null));
    }

    [Fact]
    public void Erkennt_den_abgezogenen_Rabatt_auf_der_WAHL_Rechnung()
    {
        // Volltext von Document Intelligence, Rechnung 965662 (WAHL Agrar): Rabatt unter der letzten Position,
        // Betrag in der Zeile darunter, danach der Summenblock; bezahlt wird per Lastschrift.
        const string volltext = "12x Mischkanüle für MUH-GLUE 160 & 250ml\n0,00 €\n0,00 €\npassend für 160ml & 250ml Kartusche\n"
            + "2,00% Rabatt aus 529,81 €\n-10,59 €\nPorto\nFracht/Verpack.\nVersicherung\nWarenwert\nMwSt. 19,0%\nEndbetrag\n"
            + "5,80 €\n519,22 €\n99,75 €\nEntsorgung\nSonderkost.\nNeb.kost. ges.\nNettobetrag\n624,77 €\n5,80 €\n525,02 €\n"
            + "Die Bezahlung erfolgt wunschgemäß per Lastschrift\nGewicht 41,3 kg\n"
            + "Wir reichen die SEPA-Lastschrift mit verkürzter Vorlagefrist ein.\nDie Belastung erfolgt frühestens am\n03.11.26.";

        SkontoErkenner.AusText(volltext).ShouldBe(new SkontoAngabe(2m, null, null));
    }

    [Theory]
    [InlineData("Rabatt 10 % -5,00 €")] // Positionsrabatt ohne Bezugsbetrag
    [InlineData("2,00% Rabatt aus 529,81 €\n-25,00 €")] // Abzug passt nicht zum Prozentsatz
    [InlineData("2,00% Rabatt aus 529,81 €\n10,59 €")] // nicht negativ
    [InlineData("2 % Skonto\nPorto -5,80 €")] // Betrag in der Folgezeile gehört zu etwas anderem
    [InlineData("Skonto 2 %\n-")]
    [InlineData("Kein Skonto 2 % -12,50 €")]
    [InlineData("Rabatt 15 % aus 100,00 €\n-15,00 €")] // über dem Höchstsatz
    public void Erkennt_keinen_abgezogenen_Nachlass(string text)
    {
        SkontoErkenner.AusText(text).ShouldBeNull();
    }

    [Fact]
    public void Bis_rechnet_Tage_ab_Rechnungsdatum()
    {
        SkontoErkenner.Bis(new SkontoAngabe(2m, 14, null), new DateOnly(2026, 11, 20)).ShouldBe(new DateOnly(2026, 12, 4));
        SkontoErkenner.Bis(new SkontoAngabe(2m, 14, null), null).ShouldBeNull();
        SkontoErkenner.Bis(new SkontoAngabe(2m, null, new DateOnly(2026, 12, 1)), null).ShouldBe(new DateOnly(2026, 12, 1));
    }

    [Fact]
    public void Betrag_ist_der_Abzug_auf_Cent_gerundet()
    {
        SkontoErkenner.Betrag(264.00m, 2m).ShouldBe(5.28m);
        SkontoErkenner.Betrag(100.25m, 2.5m).ShouldBe(2.51m);
        SkontoErkenner.Betrag(null, 2m).ShouldBeNull();
        SkontoErkenner.Betrag(100m, null).ShouldBeNull();
    }
}
