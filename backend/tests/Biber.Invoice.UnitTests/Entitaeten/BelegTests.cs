using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.UnitTests.Entitaeten;

public sealed class BelegTests
{
    [Fact]
    public void Loeschsperre_endet_am_Jahresende_nach_Ablauf_der_Frist()
    {
        var beleg = new Beleg { Rechnungsdatum = new DateOnly(2026, 11, 19) };

        beleg.LoeschsperreBis(8).ShouldBe(new DateOnly(2034, 12, 31));
    }

    [Fact]
    public void Ohne_Rechnungsdatum_zaehlt_das_Importjahr()
    {
        var beleg = new Beleg { ErstelltAm = new DateTimeOffset(2025, 3, 4, 8, 0, 0, TimeSpan.Zero) };

        beleg.LoeschsperreBis(10).ShouldBe(new DateOnly(2035, 12, 31));
    }
}
