using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.UnitTests.Entitaeten;

public sealed class MatchingTests
{
    private static bool Trifft(MatchingAlgorithmus algorithmus, string? muster, string? text, bool grossKlein = false) =>
        new Matching { Algorithmus = algorithmus, Muster = muster, GrossKlein = grossKlein }.Trifft(text);

    [Theory]
    [InlineData("Toner", "Toner schwarz")]
    [InlineData("toner", "TONER schwarz")]
    [InlineData("", "Toner")]
    [InlineData(null, "Toner")]
    public void Keiner_trifft_nie(string? muster, string text) =>
        Trifft(MatchingAlgorithmus.Keiner, muster, text).ShouldBeFalse();

    [Theory]
    [InlineData("Toner", "Toner schwarz")]
    [InlineData("Krämer", "Krämer KG")]
    [InlineData("", "Toner")]
    public void Auto_trifft_nie(string muster, string text) =>
        Trifft(MatchingAlgorithmus.Auto, muster, text).ShouldBeFalse();

    [Theory]
    [InlineData("telekom vodafone", "Rechnung der Telekom Deutschland", true)]
    [InlineData("telekom vodafone", "Vodafone GmbH", true)]
    [InlineData("telekom vodafone", "Telekommunikation", false)]
    [InlineData("strom gas", "Stadtwerke Wasser", false)]
    public void EinWort_trifft_bei_einem_ganzen_Wort(string muster, string text, bool erwartet) =>
        Trifft(MatchingAlgorithmus.EinWort, muster, text).ShouldBe(erwartet);

    [Theory]
    [InlineData("toner patrone", "Toner-Patrone schwarz", true)]
    [InlineData("toner patrone", "Patrone für Toner", true)]
    [InlineData("toner patrone", "Toner schwarz", false)]
    [InlineData("toner patrone", "Tonerpatrone", false)]
    public void AlleWoerter_braucht_jedes_Wort(string muster, string text, bool erwartet) =>
        Trifft(MatchingAlgorithmus.AlleWoerter, muster, text).ShouldBe(erwartet);

    [Theory]
    [InlineData("Krämer KG", "Rechnung Krämer KG, Bonn", true)]
    [InlineData("krämer kg", "Rechnung KRÄMER KG, Bonn", true)]
    [InlineData("Krämer KG", "Krämer GmbH", false)]
    [InlineData("Kraemer KG", "KG Kraemer", false)]
    public void Exakt_sucht_die_Zeichenfolge(string muster, string text, bool erwartet) =>
        Trifft(MatchingAlgorithmus.Exakt, muster, text).ShouldBe(erwartet);

    [Theory]
    [InlineData(@"RE-\d{4}", "Nr. RE-2026", true)]
    [InlineData(@"^amazon", "AMAZON EU S.a.r.l.", true)]
    [InlineData(@"RE-\d{4}", "Nr. RE-20", false)]
    [InlineData(@"([a-z", "irgendwas", false)]
    public void Regex_trifft_und_ungueltiger_Ausdruck_nie(string muster, string text, bool erwartet) =>
        Trifft(MatchingAlgorithmus.Regex, muster, text).ShouldBe(erwartet);

    [Fact]
    public void Regex_mit_Zeitueberschreitung_trifft_nicht()
    {
        var text = new string('a', 30_000) + "!";

        Trifft(MatchingAlgorithmus.Regex, "(a+)+$", text).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Bürobedarf Schmidt", "Lieferung von Burobedarf Schmidt GmbH", true)]
    [InlineData("Telekom Deutschland", "Rechnung Telekon Deutschland GmbH", true)]
    [InlineData("Telekom Deutschland", "Vodafone GmbH", false)]
    [InlineData("Stadtwerke", "Stadtbibliothek", false)]
    public void Unscharf_trifft_ab_85_Prozent_Aehnlichkeit(string muster, string text, bool erwartet) =>
        Trifft(MatchingAlgorithmus.Unscharf, muster, text).ShouldBe(erwartet);

    [Theory]
    [InlineData(MatchingAlgorithmus.EinWort)]
    [InlineData(MatchingAlgorithmus.AlleWoerter)]
    [InlineData(MatchingAlgorithmus.Exakt)]
    [InlineData(MatchingAlgorithmus.Regex)]
    [InlineData(MatchingAlgorithmus.Unscharf)]
    public void GrossKlein_beachtet_die_Schreibweise(MatchingAlgorithmus algorithmus)
    {
        Trifft(algorithmus, "Toner", "TONER schwarz", grossKlein: true).ShouldBeFalse();
        Trifft(algorithmus, "Toner", "Toner schwarz", grossKlein: true).ShouldBeTrue();
        Trifft(algorithmus, "Toner", "TONER schwarz").ShouldBeTrue();
    }

    [Theory]
    [InlineData(MatchingAlgorithmus.EinWort)]
    [InlineData(MatchingAlgorithmus.Exakt)]
    [InlineData(MatchingAlgorithmus.Unscharf)]
    public void Leerer_Text_trifft_nie(MatchingAlgorithmus algorithmus)
    {
        Trifft(algorithmus, "Toner", null).ShouldBeFalse();
        Trifft(algorithmus, "Toner", "").ShouldBeFalse();
    }
}
