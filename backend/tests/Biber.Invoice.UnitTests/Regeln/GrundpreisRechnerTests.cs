using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class GrundpreisRechnerTests
{
    [Theory]
    [InlineData("Kaffee 500 g", 1, "Stück", 6.99, 13.98, "kg")]
    [InlineData("Wasser 6 × 1,5 l", 1, "Stück", 4.50, 0.50, "l")]
    [InlineData("Wasser 6 x 1,5 l", 1, null, 4.50, 0.50, "l")]
    [InlineData("Saft 0,75l", 1, "C62", 2.25, 3.00, "l")]
    [InlineData("Cola 6 × 0,33 l", 1, "C62", 3.96, 2.00, "l")]
    [InlineData("Nudeln 5 x 500 g", 1, "Stk", 5.00, 2.00, "kg")]
    [InlineData("Mehl 1 kg", 2, "Stück", 0.89, 0.89, "kg")]
    [InlineData("Sahne 200 ml", 1, null, 0.79, 3.95, "l")]
    [InlineData("Likör 70 cl", 1, null, 14.00, 20.00, "l")]
    [InlineData("Kabel 50 cm", 1, null, 2.00, 4.00, "m")]
    [InlineData("Klebeband 66 m", 1, null, 3.30, 0.05, "m")]
    [InlineData("Draht 250 mm", 1, null, 1.00, 4.00, "m")]
    [InlineData("Kartoffeln lose", 3.5, "kg", 1.29, 1.29, "kg")]
    [InlineData("Käse am Stück", 0.25, "KGM", 19.90, 19.90, "kg")]
    [InlineData("Diesel", 40, "LTR", 1.65, 1.65, "l")]
    [InlineData("Hackfleisch", 400, "g", 0.01, 10.00, "kg")]
    [InlineData("Kugelschreiber blau", 10, "Stück", 0.99, 0.99, "Stück")]
    [InlineData("Kugelschreiber blau", 10, "St.", 0.99, 0.99, "Stück")]
    [InlineData("Kugelschreiber blau", 10, "C62", 0.99, 0.99, "Stück")]
    [InlineData("Kaffee 1/3 Pfund", 1, "Stk", 4.00, 4.00, "Stück")]
    [InlineData("Kaffee 333 g", 1, null, 1.00, 3.00, "kg")]
    public void Berechnet(string text, double menge, string? einheit, double einzelpreis, double preis, string grundeinheit)
    {
        var ergebnis = GrundpreisRechner.Berechnen(text, (decimal)menge, einheit, (decimal)einzelpreis);

        ergebnis.ShouldBe(new Grundpreis((decimal)preis, grundeinheit));
    }

    [Theory]
    [InlineData("Beratung", 2, "Std")]
    [InlineData("Kopierpapier A4 80 g/m²", 1, null)]
    [InlineData("Wartungspauschale", 1, null)]
    [InlineData("Artikel 4711", 1, "Paket")]
    public void Ohne_erkennbare_Groesse_gibt_es_keinen_Grundpreis(string text, double menge, string? einheit)
    {
        GrundpreisRechner.Berechnen(text, (decimal)menge, einheit, 10m).ShouldBeNull();
    }

    [Fact]
    public void Ohne_Einzelpreis_gibt_es_keinen_Grundpreis()
    {
        GrundpreisRechner.Berechnen("Kaffee 500 g", 1, "Stück", null).ShouldBeNull();
    }

    [Fact]
    public void Mit_Umrechnungsfaktor_gilt_die_Basiseinheit_des_Stammartikels()
    {
        GrundpreisRechner.Berechnen("Kopierpapier Karton", 1, "Karton", 24.90m, 2500m, 100m, "Blatt")
            .ShouldBe(new Grundpreis(1.00m, "100 Blatt"));
    }

    [Fact]
    public void Mit_Umrechnungsfaktor_und_Bezugsmenge_1_nur_die_Basiseinheit()
    {
        GrundpreisRechner.Berechnen("Wasser Kiste 12 x 1 l", 1, "Kiste", 6.00m, 12m, 1m, "Flasche")
            .ShouldBe(new Grundpreis(0.50m, "Flasche"));
    }

    [Fact]
    public void Rundet_kaufmaennisch_auf_Cent()
    {
        GrundpreisRechner.Berechnen("Probe 8 g", 1, null, 0.01m).ShouldBe(new Grundpreis(1.25m, "kg"));
        GrundpreisRechner.Berechnen("Tee 3 x 1 kg", 1, null, 1.00m).ShouldBe(new Grundpreis(0.33m, "kg"));
    }
}
