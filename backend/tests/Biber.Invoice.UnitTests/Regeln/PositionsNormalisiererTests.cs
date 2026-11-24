using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class PositionsNormalisiererTests
{
    [Theory]
    [InlineData("Kaffee Crema 1 kg", "kaffee crema 1 kg")]
    [InlineData("KAFFEE CREMA 1 KG", "kaffee crema 1 kg")]
    [InlineData("123456 Kaffee Crema", "kaffee crema")]
    [InlineData("1234-Kaffee Crema", "kaffee crema")]
    [InlineData("98765: Kaffee Crema", "kaffee crema")]
    [InlineData("123 Kaffee", "123 kaffee")]
    [InlineData("Kaffee    Crema\t 1 kg", "kaffee crema 1 kg")]
    [InlineData("  Kaffee Crema  ", "kaffee crema")]
    [InlineData("- Kaffee Crema.", "kaffee crema")]
    [InlineData("\"Kaffee Crema\"", "kaffee crema")]
    [InlineData("Kopierpapier A4, 80 g/m²", "kopierpapier a4, 80 g/m2")]
    [InlineData("Wasser 6 × 1,5 l", "wasser 6 × 1,5 l")]
    [InlineData("Wasser 6 x 0,5 l", "wasser 6 x 0,5 l")]
    [InlineData("Ｋａｆｆｅｅ", "kaffee")]
    [InlineData("Rabatt 10 %", "rabatt 10 %")]
    [InlineData("Ölfilter (groß)", "ölfilter (groß)")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("1234 ", "1234")]
    public void Normalisiert(string text, string erwartet)
    {
        PositionsNormalisierer.Normalisieren(text).ShouldBe(erwartet);
    }

    [Fact]
    public void Groessenangaben_unterscheiden_Artikel()
    {
        PositionsNormalisierer.Normalisieren("Kaffee 500 g").ShouldNotBe(PositionsNormalisierer.Normalisieren("Kaffee 1 kg"));
    }

    [Fact]
    public void Anzeigename_ist_getrimmt_und_hoechstens_200_Zeichen()
    {
        PositionsNormalisierer.Anzeigename("  Kaffee  ").ShouldBe("Kaffee");
        PositionsNormalisierer.Anzeigename(new string('a', 250)).Length.ShouldBe(200);
    }
}
