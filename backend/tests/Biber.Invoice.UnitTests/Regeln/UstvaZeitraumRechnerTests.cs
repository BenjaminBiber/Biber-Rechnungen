using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class UstvaZeitraumRechnerTests
{
    [Fact]
    public void Monate_eines_Quartals()
    {
        var zeitraeume = UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), UstvaZeitraum.Monatlich);

        zeitraeume.ShouldBe([
            (new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31), "Juli 2026"),
            (new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), "August 2026"),
            (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), "September 2026"),
        ]);
    }

    [Fact]
    public void Quartale_eines_Jahres()
    {
        var zeitraeume = UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), UstvaZeitraum.Quartalsweise);

        zeitraeume.ShouldBe([
            (new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), "Q1 2026"),
            (new DateOnly(2026, 4, 1), new DateOnly(2026, 6, 30), "Q2 2026"),
            (new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), "Q3 2026"),
            (new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), "Q4 2026"),
        ]);
    }

    [Fact]
    public void Jahr()
    {
        var zeitraeume = UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), UstvaZeitraum.Jaehrlich);

        zeitraeume.ShouldBe([(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "2026")]);
    }

    [Fact]
    public void Ueber_den_Jahreswechsel()
    {
        var monate = UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 31), UstvaZeitraum.Monatlich);
        var quartale = UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 10, 1), new DateOnly(2027, 3, 31), UstvaZeitraum.Quartalsweise);
        var jahre = UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 10, 1), new DateOnly(2027, 3, 31), UstvaZeitraum.Jaehrlich);

        monate.Select(z => z.Bezeichnung).ShouldBe(["Dezember 2026", "Januar 2027"]);
        quartale.Select(z => z.Bezeichnung).ShouldBe(["Q4 2026", "Q1 2027"]);
        jahre.ShouldBe([
            (new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), "2026"),
            (new DateOnly(2027, 1, 1), new DateOnly(2027, 3, 31), "2027"),
        ]);
    }

    [Fact]
    public void Angeschnittene_Zeitraeume_werden_auf_die_Auswahl_begrenzt()
    {
        var zeitraeume = UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 2, 15), new DateOnly(2026, 5, 10), UstvaZeitraum.Quartalsweise);

        zeitraeume.ShouldBe([
            (new DateOnly(2026, 2, 15), new DateOnly(2026, 3, 31), "Q1 2026"),
            (new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 10), "Q2 2026"),
        ]);
    }

    [Fact]
    public void Ein_einzelner_Tag_ergibt_einen_Zeitraum()
    {
        var zeitraeume = UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 2, 28), new DateOnly(2026, 2, 28), UstvaZeitraum.Monatlich);

        zeitraeume.ShouldBe([(new DateOnly(2026, 2, 28), new DateOnly(2026, 2, 28), "Februar 2026")]);
    }

    [Fact]
    public void Von_nach_Bis_ergibt_keinen_Zeitraum()
    {
        UstvaZeitraumRechner.Zeitraeume(new DateOnly(2026, 3, 1), new DateOnly(2026, 2, 1), UstvaZeitraum.Monatlich).ShouldBeEmpty();
    }
}
