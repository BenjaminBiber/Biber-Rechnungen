using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class AblagePfadBilderTests
{
    private static readonly DateOnly Datum = new(2026, 3, 14);

    public static TheoryData<string?, string?, string, string> Faelle => new()
    {
        { "Bürowelt Krämer KG", "RE-2026-0912", "pdf", "2026/Buerowelt_Kraemer_KG/2026-03-14_RE-2026-0912.pdf" },
        { "Öko Ärger Übel", "1", "pdf", "2026/Oeko_Aerger_Uebel/2026-03-14_1.pdf" },
        { "Straße & Söhne GmbH", "A1", "xml", "2026/Strasse_Soehne_GmbH/2026-03-14_A1.xml" },
        { "ACME, Inc. (US)", "X", "pdf", "2026/ACME_Inc_US/2026-03-14_X.pdf" },
        { "  __Rand__  ", "X", "pdf", "2026/Rand/2026-03-14_X.pdf" },
        { null, "RE-1", "pdf", "2026/_Unbekannt/2026-03-14_RE-1.pdf" },
        { "", "RE-1", "pdf", "2026/_Unbekannt/2026-03-14_RE-1.pdf" },
        { "!!!", "RE-1", "pdf", "2026/_Unbekannt/2026-03-14_RE-1.pdf" },
        { "Firma", null, "pdf", "2026/Firma/2026-03-14_ohne-Nummer.pdf" },
        { "Firma", "   ", "pdf", "2026/Firma/2026-03-14_ohne-Nummer.pdf" },
        { "Firma", "RE/2026/01", "pdf", "2026/Firma/2026-03-14_RE_2026_01.pdf" },
        { "Firma", "../../etc", "pdf", "2026/Firma/2026-03-14_etc.pdf" },
        { "Firma", "..\\..\\win", "pdf", "2026/Firma/2026-03-14_win.pdf" },
        { "Firma", "RE 1.2", "PDF", "2026/Firma/2026-03-14_RE_1_2.pdf" },
        { "Firma", "R", "tiff", "2026/Firma/2026-03-14_R.tiff" },
        { new string('a', 80), new string('7', 70), "jpg", $"2026/{new string('a', 60)}/2026-03-14_{new string('7', 60)}.jpg" },
        { new string('a', 59) + " b", "1", "png", $"2026/{new string('a', 59)}/2026-03-14_1.png" },
    };

    [Theory]
    [MemberData(nameof(Faelle))]
    public void Bildet_Pfad_nach_Muster(string? korrespondent, string? nummer, string endung, string erwartet)
    {
        AblagePfadBilder.Bilden(2026, korrespondent, Datum, nummer, endung).ShouldBe(erwartet);
    }

    [Fact]
    public void Jahr_kommt_aus_dem_Parameter()
    {
        AblagePfadBilder.Bilden(2025, "Firma", Datum, "1", "pdf").ShouldBe("2025/Firma/2026-03-14_1.pdf");
    }
}
