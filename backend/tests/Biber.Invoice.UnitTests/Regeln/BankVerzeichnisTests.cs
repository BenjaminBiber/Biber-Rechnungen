using Biber.Invoice.Infrastructure.Module.Banken;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class BankVerzeichnisTests
{
    [Theory]
    [InlineData("DE55733692640000043591", "73369264")]
    [InlineData("DE44 7332 0073 0043 6038 76", "73320073")]
    [InlineData("de89 3704 0044 0532 0130 00", "37040044")]
    public void Bankleitzahl_einer_deutschen_IBAN(string iban, string blz)
    {
        BankVerzeichnis.Blz(iban).ShouldBe(blz);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GB82WEST12345698765432")]
    [InlineData("DE55733692640000043592")]
    [InlineData("DE5573369264")]
    public void Keine_Bankleitzahl_bei_auslaendischer_oder_ungueltiger_IBAN(string? iban)
    {
        BankVerzeichnis.Blz(iban).ShouldBeNull();
    }
}
