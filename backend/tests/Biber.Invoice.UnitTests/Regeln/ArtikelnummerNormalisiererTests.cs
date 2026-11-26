using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class ArtikelnummerNormalisiererTests
{
    [Theory]
    [InlineData("93435", "93435")]
    [InlineData(" 5g12fdlorx45 ", "5G12FDLORX45")]
    [InlineData("AB 12-3", "AB12-3")]
    [InlineData("Art.-Nr. 4711", "4711")]
    [InlineData("Artikel-Nr.: 37/8", "37/8")]
    [InlineData("Artikelnummer 385/65R22,5S", "385/65R22,5S")]
    [InlineData("ＦＲ", "FR")]
    public void Normalisieren_liefert_den_Schluessel(string nummer, string schluessel) =>
        ArtikelnummerNormalisierer.Normalisieren(nummer).ShouldBe(schluessel);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("-")]
    [InlineData("Art.-Nr.")]
    public void Ohne_Buchstaben_oder_Ziffern_gibt_es_keine_Nummer(string? nummer)
    {
        ArtikelnummerNormalisierer.Normalisieren(nummer).ShouldBeNull();
        ArtikelnummerNormalisierer.Bereinigen(nummer).ShouldBeNull();
    }

    [Fact]
    public void Bereinigen_behaelt_die_Schreibweise()
    {
        ArtikelnummerNormalisierer.Bereinigen("  ab   12-3 ").ShouldBe("ab 12-3");
        ArtikelnummerNormalisierer.Bereinigen("Art. Nr: SI-NW4").ShouldBe("SI-NW4");
    }

    [Theory]
    [InlineData(" XL ", "XL")]
    [InlineData("9/10", "9/10")]
    [InlineData("schwarz \n XL", "schwarz XL")]
    [InlineData("-", null)]
    [InlineData(null, null)]
    public void Variante_wird_getrimmt(string? variante, string? erwartet) =>
        ArtikelnummerNormalisierer.Variante(variante).ShouldBe(erwartet);

    [Fact]
    public void Lange_Angaben_werden_gekuerzt()
    {
        ArtikelnummerNormalisierer.Normalisieren(new string('A', 150))!.Length.ShouldBe(ArtikelnummerNormalisierer.MaxLaenge);
        ArtikelnummerNormalisierer.Variante(new string('x', 150))!.Length.ShouldBe(ArtikelnummerNormalisierer.MaxLaenge);
    }
}
