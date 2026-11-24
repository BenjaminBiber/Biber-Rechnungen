using Biber.Invoice.Infrastructure.Module.Aufteilen;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class PdfTeilerTests
{
    [Fact]
    public void Extrahiert_die_angegebenen_Seiten_in_ein_neues_PDF()
    {
        using var quelle = new MemoryStream(VierseitigesPdf());

        var teil = new PdfTeiler().Extrahieren(quelle, [3, 4]);

        using var pdf = PdfDocument.Open(teil);
        pdf.NumberOfPages.ShouldBe(2);
        pdf.GetPage(1).Text.ShouldContain("Seite 3");
        pdf.GetPage(2).Text.ShouldContain("Seite 4");
    }

    [Fact]
    public void Haelt_die_angegebene_Reihenfolge_ein()
    {
        using var quelle = new MemoryStream(VierseitigesPdf());

        var teil = new PdfTeiler().Extrahieren(quelle, [2, 1]);

        using var pdf = PdfDocument.Open(teil);
        pdf.GetPage(1).Text.ShouldContain("Seite 2");
        pdf.GetPage(2).Text.ShouldContain("Seite 1");
    }

    [Fact]
    public void Lehnt_Seiten_ausserhalb_des_PDFs_ab()
    {
        using var quelle = new MemoryStream(VierseitigesPdf());

        Should.Throw<ArgumentOutOfRangeException>(() => new PdfTeiler().Extrahieren(quelle, [5]));
    }

    /// <summary>Vier Seiten mit dem Text „Seite n“.</summary>
    internal static byte[] VierseitigesPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        return Document.Create(dokument =>
        {
            for (var i = 1; i <= 4; i++)
            {
                var n = i;
                dokument.Page(seite => seite.Content().Text($"Seite {n}"));
            }
        }).GeneratePdf();
    }
}
