using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.UnitTests.Entitaeten;

public sealed class KategorieTests
{
    private static readonly Kategorie Kfz = new() { Name = "Kfz", EuerZeile = "Zeile 46 – Kfz-Kosten" };

    [Fact]
    public void Unterkategorie_ohne_eigene_EUeR_Zeile_erbt_die_der_Oberkategorie()
    {
        var tanken = new Kategorie { Name = "Tanken", Eltern = Kfz, EuerZeile = " " };

        tanken.WirksameEuerZeile.ShouldBe("Zeile 46 – Kfz-Kosten");
        tanken.VollerName.ShouldBe("Kfz › Tanken");
    }

    [Fact]
    public void Eigene_EUeR_Zeile_geht_vor()
    {
        var leasing = new Kategorie { Name = "Leasing", Eltern = Kfz, EuerZeile = "Zeile 47" };

        leasing.WirksameEuerZeile.ShouldBe("Zeile 47");
    }

    [Fact]
    public void Hauptkategorie_ohne_EUeR_Zeile_hat_keine()
    {
        var buero = new Kategorie { Name = "Büro" };

        buero.WirksameEuerZeile.ShouldBeNull();
        buero.VollerName.ShouldBe("Büro");
    }
}
