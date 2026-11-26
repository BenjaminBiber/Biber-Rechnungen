using Biber.Invoice.Infrastructure.Module.GutschriftBezug.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class GutschriftBezugFinderTests
{
    private static readonly Guid R1 = Guid.NewGuid();
    private static readonly Guid R2 = Guid.NewGuid();

    private static readonly RechnungKandidat[] Kandidaten =
    [
        new(R1, "RE-2026-0815"),
        new(R2, "RE-2026-0816"),
    ];

    [Fact]
    public void Bezugsnummer_aus_der_E_Rechnung_gewinnt_auch_mit_anderer_Schreibweise()
    {
        GutschriftBezugFinder.Finden("RE 2026 0816", "Gutschrift zu RE-2026-0815", "GS-1", Kandidaten).ShouldBe(R2);
    }

    [Fact]
    public void Findet_die_Rechnungsnummer_im_Volltext()
    {
        GutschriftBezugFinder.Finden(null, "Gutschrift zur Rechnung RE-2026-0815 vom 03.11.2026", "GS-1", Kandidaten).ShouldBe(R1);
    }

    [Fact]
    public void Teilwort_zaehlt_nicht()
    {
        GutschriftBezugFinder.Finden(null, "Bezug: XRE-2026-08150", "GS-1", Kandidaten).ShouldBeNull();
    }

    [Fact]
    public void Mehrdeutig_ergibt_keinen_Bezug()
    {
        GutschriftBezugFinder.Finden(null, "Korrektur zu RE-2026-0815 und RE-2026-0816", "GS-1", Kandidaten).ShouldBeNull();
    }

    [Fact]
    public void Zu_kurze_Nummern_werden_im_Text_nicht_gesucht()
    {
        var kurz = new RechnungKandidat[] { new(R1, "12") };

        GutschriftBezugFinder.Finden(null, "Pos. 12 Gutschrift", "GS-1", kurz).ShouldBeNull();
        GutschriftBezugFinder.Finden("12", null, "GS-1", kurz).ShouldBe(R1);
    }

    [Fact]
    public void Die_eigene_Nummer_ist_kein_Bezug()
    {
        GutschriftBezugFinder.Finden(null, "Gutschrift RE-2026-0815", "RE-2026-0815", [new(R1, "RE-2026-0815")]).ShouldBeNull();
    }
}
