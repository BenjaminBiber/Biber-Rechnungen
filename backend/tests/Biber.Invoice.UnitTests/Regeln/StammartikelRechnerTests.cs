using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class StammartikelRechnerTests
{
    [Fact]
    public void Karton_Kopierpapier_kostet_einen_Euro_je_100_Blatt()
    {
        StammartikelRechner.Grundpreis(24.90m, 2500m, 100m).ShouldBe(1.00m);
    }

    [Fact]
    public void Pack_Kopierpapier_kostet_1_10_je_100_Blatt()
    {
        StammartikelRechner.Grundpreis(5.49m, 500m, 100m).ShouldBe(1.10m);
    }

    [Fact]
    public void Grundpreis_wird_kaufmaennisch_auf_Cent_gerundet()
    {
        StammartikelRechner.Grundpreis(1.00m, 8m, 1m).ShouldBe(0.13m);
        StammartikelRechner.Grundpreis(1.00m, 3m, 1m).ShouldBe(0.33m);
    }

    [Fact]
    public void Ohne_Preis_oder_Faktor_gibt_es_keinen_Grundpreis()
    {
        StammartikelRechner.Grundpreis(null, 2500m, 100m).ShouldBeNull();
        StammartikelRechner.Grundpreis(24.90m, null, 100m).ShouldBeNull();
        StammartikelRechner.Grundpreis(24.90m, 0m, 100m).ShouldBeNull();
    }

    [Fact]
    public void Menge_in_Basiseinheiten_ist_Menge_mal_Faktor()
    {
        var karton = StammartikelRechner.MengeInBasiseinheiten(9m, 2500m);
        var pack = StammartikelRechner.MengeInBasiseinheiten(2m, 500m);

        (karton + pack).ShouldBe(23500m);
    }

    [Fact]
    public void Menge_ohne_Faktor_zaehlt_nicht()
    {
        StammartikelRechner.MengeInBasiseinheiten(3m, null).ShouldBe(0m);
        StammartikelRechner.MengeInBasiseinheiten(null, 500m).ShouldBe(0m);
    }

    [Theory]
    [InlineData(100, "Blatt", "100 Blatt")]
    [InlineData(1, "Blatt", "Blatt")]
    [InlineData(0.5, "kg", "0,5 kg")]
    public void Grundpreiseinheit_nennt_die_Bezugsmenge(decimal menge, string basiseinheit, string erwartet)
    {
        StammartikelRechner.GrundpreisEinheit(menge, basiseinheit).ShouldBe(erwartet);
    }

    [Fact]
    public void Aenderung_ist_der_Anteil_neu_gegen_alt()
    {
        StammartikelRechner.Aenderung(22.99m, 24.90m).ShouldBe(0.0831m);
        StammartikelRechner.Aenderung(2.00m, 1.50m).ShouldBe(-0.25m);
    }

    [Fact]
    public void Aenderung_ohne_Ausgangspreis_ist_unbekannt()
    {
        StammartikelRechner.Aenderung(null, 1m).ShouldBeNull();
        StammartikelRechner.Aenderung(0m, 1m).ShouldBeNull();
        StammartikelRechner.Aenderung(1m, null).ShouldBeNull();
    }
}
