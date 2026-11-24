using Biber.Invoice.Infrastructure.Module.KorrespondentZuordnung.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class KennungNormalisiererTests
{
    [Theory]
    [InlineData("DE 284 117 560", "DE284117560")]
    [InlineData("de-284.117.560", "DE284117560")]
    [InlineData("ATU12345678", "ATU12345678")]
    public void UstIdNr_wird_ohne_Leer_und_Sonderzeichen_gross_geschrieben(string wert, string erwartet)
    {
        KennungNormalisierer.UstIdNr(wert).ShouldBe(erwartet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" - ")]
    public void Leere_UstIdNr_ergibt_null(string? wert)
    {
        KennungNormalisierer.UstIdNr(wert).ShouldBeNull();
    }

    [Fact]
    public void Iban_wird_ohne_Leerzeichen_gross_geschrieben()
    {
        KennungNormalisierer.Iban("de89 3704 0044 0532 0130 00").ShouldBe("DE89370400440532013000");
        KennungNormalisierer.Iban(" ").ShouldBeNull();
        KennungNormalisierer.Iban(null).ShouldBeNull();
    }

    [Theory]
    [InlineData("Bürowelt Krämer KG", "Buerowelt Kraemer")]
    [InlineData("Müller & Co. KG", "Mueller")]
    [InlineData("Hans Groß e.K.", "hans gross")]
    [InlineData("Schreib-Bedarf UG (haftungsbeschränkt)", "schreib bedarf")]
    [InlineData("Office  Partner GmbH & Co. KG", "Office Partner")]
    [InlineData("Druck AG", "druck")]
    [InlineData("Papier mbH", "papier")]
    public void Namen_mit_Rechtsform_und_Umlauten_sind_gleich(string a, string b)
    {
        KennungNormalisierer.Name(a).ShouldBe(KennungNormalisierer.Name(b));
    }

    [Fact]
    public void Name_wird_klein_und_ohne_Satzzeichen()
    {
        KennungNormalisierer.Name("Bürowelt  Krämer, KG!").ShouldBe("buerowelt kraemer");
    }
}
