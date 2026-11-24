using Biber.Invoice.Infrastructure.Module.FachlicheDublette.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class RechnungsnummerNormalisiererTests
{
    [Theory]
    [InlineData("2026-48817", "202648817")]
    [InlineData("2026 48817", "202648817")]
    [InlineData("2026/48.817", "202648817")]
    [InlineData("re-2026 / 0912", "RE20260912")]
    [InlineData("  AB.12-cd  ", "AB12CD")]
    public void Trennzeichen_entfallen_und_Buchstaben_werden_gross(string wert, string erwartet)
    {
        RechnungsnummerNormalisierer.Normalisieren(wert).ShouldBe(erwartet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(" - / . ")]
    public void Leere_Nummer_ergibt_null(string? wert)
    {
        RechnungsnummerNormalisierer.Normalisieren(wert).ShouldBeNull();
    }
}
