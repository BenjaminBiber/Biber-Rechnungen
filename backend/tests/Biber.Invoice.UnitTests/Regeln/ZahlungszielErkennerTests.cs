using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class ZahlungszielErkennerTests
{
    private static readonly DateOnly Rechnungsdatum = new(2026, 10, 7);

    [Theory]
    [InlineData("Bitte zahlen Sie innerhalb von 14Tagen auf das angegebene Konto.", 14)]
    [InlineData("Zahlbar innerhalb von 30 Tagen ohne Abzug.", 30)]
    [InlineData("Zahlungsziel: 10 Tage", 10)]
    [InlineData("Der Rechnungsbetrag ist innerhalb von vierzehn Tagen fällig.", 14)]
    [InlineData("Zahlbar innerhalb von zwei Wochen nach Rechnungserhalt.", 14)]
    [InlineData("Bitte überweisen Sie den Betrag binnen 21 Kalendertagen.", 21)]
    [InlineData("Zahlbar sofort ohne Abzug.", 0)]
    [InlineData("Der Betrag ist sofort nach Erhalt der Rechnung fällig.", 0)]
    [InlineData("Bitte überweisen Sie den Betrag innerhalb von\n14 Tagen auf unser Konto.", 14)]
    [InlineData("Zahlungsbedingungen:\n30 Tage netto", 30)]
    [InlineData("Bitte zahlen Sie innerhalb von 14 Tagen. Bei Zahlungsverzug berechnen wir nach 30 Tagen Mahngebühren.", 14)]
    [InlineData("Zahlbar innerhalb 14 Tagen nach Erhalt, Rückgabe innerhalb 30 Tagen", 14)]
    public void Frist_in_Tagen_zaehlt_ab_Rechnungsdatum(string text, int tage)
    {
        ZahlungszielErkenner.Faelligkeit(text, Rechnungsdatum).ShouldBe(Rechnungsdatum.AddDays(tage));
    }

    [Theory]
    [InlineData("Zahlungsbedingungen: 14 Tage 2% Skonto, 30 Tage netto")]
    [InlineData("Zahlbar innerhalb von 14 Tagen mit 2 % Skonto, 30 Tage netto.")]
    [InlineData("Zahlbar innerhalb 10 Tagen mit 2 % Skonto oder 30 Tagen ohne Skonto")]
    [InlineData("Zahlbar innerhalb von 30 Tagen, bei Zahlung innerhalb von 10 Tagen 2 % Skonto.")]
    [InlineData("14 Tage 3 %, 30 Tage rein netto")]
    public void Neben_Skonto_gilt_das_Nettoziel(string text)
    {
        ZahlungszielErkenner.Faelligkeit(text, Rechnungsdatum).ShouldBe(Rechnungsdatum.AddDays(30));
    }

    [Theory]
    [InlineData("Zahlbar bis 21.10.2026", 2026, 10, 21)]
    [InlineData("Bitte begleichen Sie den Betrag bis zum 28. Oktober 2026.", 2026, 10, 28)]
    [InlineData("Fälligkeitsdatum: 30.10.26", 2026, 10, 30)]
    [InlineData("Fällig am:\n02.11.2026", 2026, 11, 2)]
    [InlineData("Bei Zahlung bis 12.10.2026 abzüglich 2 % Skonto, bis 31.10.2026 netto", 2026, 10, 31)]
    [InlineData("Leistung vom 01.10.2026 bis 31.10.2026, zahlbar bis 14.10.2026", 2026, 10, 14)]
    public void Festes_Datum(string text, int jahr, int monat, int tag)
    {
        ZahlungszielErkenner.Faelligkeit(text, Rechnungsdatum).ShouldBe(new DateOnly(jahr, monat, tag));
    }

    [Fact]
    public void Festes_Datum_auch_ohne_Rechnungsdatum_Tage_nicht()
    {
        ZahlungszielErkenner.Faelligkeit("Zahlbar bis 21.10.2026", null).ShouldBe(new DateOnly(2026, 10, 21));
        ZahlungszielErkenner.Faelligkeit("Zahlbar innerhalb von 14 Tagen", null).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bei Zahlung innerhalb von 10 Tagen gewähren wir 3 % Skonto.")]
    [InlineData("Zahlbar innerhalb 8 Tagen, 2 % Skonto")]
    [InlineData("Lieferzeit 14 Tage")]
    [InlineData("Sie können den Vertrag innerhalb von 14 Tagen widerrufen. Zahlung per Rechnung.")]
    [InlineData("Beanstandungen bitte innerhalb von 8 Tagen, Zahlung per Überweisung")]
    [InlineData("Leistung vom 01.10.2026 bis 31.10.2026, Zahlung per Lastschrift")]
    [InlineData("Nach Zahlungseingang wird die Ware sofort versandt.")]
    [InlineData("Zahlbar bis 01.10.2026")]
    [InlineData("Bitte geben Sie bei Überweisungen die\nRechnungsnummer 20433/2086 und das\nRechnungsdatum an.")]
    public void Erkennt_kein_Zahlungsziel(string? text)
    {
        ZahlungszielErkenner.Faelligkeit(text, Rechnungsdatum).ShouldBeNull();
    }
}
