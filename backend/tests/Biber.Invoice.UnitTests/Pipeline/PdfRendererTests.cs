using Biber.Invoice.Infrastructure.Module.Vorschau;
using SkiaSharp;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class PdfRendererTests
{
    private readonly PdfRenderer _renderer = new();

    // PDFsharp 6.5 verlangt zum Erzeugen eine Lizenz; die Testdateien schreiben das PDF selbst (A4, leere Seiten).
    private static byte[] TestPdf(int seiten) => Testdateien.Formate.Testdateien.Pdf(seiten);

    [Fact]
    public void Seitenanzahl_eines_dreiseitigen_PDFs_ist_3()
    {
        _renderer.Seitenanzahl(new MemoryStream(TestPdf(3))).ShouldBe(3);
    }

    [Fact]
    public void Seite_2_ergibt_PNG_mit_verlangter_Breite()
    {
        var png = _renderer.SeiteAlsPng(new MemoryStream(TestPdf(3)), 2, 300);

        png.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }).ShouldBeTrue();
        using var bild = SKBitmap.Decode(png);
        bild.Width.ShouldBe(300);
        bild.Height.ShouldBeInRange(420, 426); // A4: 595 × 842 pt
        bild.GetPixel(5, 5).ShouldBe(SKColors.White);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Ungueltige_Seite_wirft(int seite)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => _renderer.SeiteAlsPng(new MemoryStream(TestPdf(3)), seite, 300));
    }
}
