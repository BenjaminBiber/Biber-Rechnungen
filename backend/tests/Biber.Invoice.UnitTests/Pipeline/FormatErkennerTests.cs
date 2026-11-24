using System.Text;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Formaterkennung;
using Dateien = Biber.Invoice.UnitTests.Testdateien.Formate.Testdateien;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class FormatErkennerTests
{
    private readonly FormatErkenner _erkenner = new();

    private FormatErgebnis Erkenne(byte[] inhalt, string name) => _erkenner.Erkennen(new MemoryStream(inhalt), name);

    [Fact]
    public void Normales_PDF_mit_Seitenanzahl()
    {
        var ergebnis = Erkenne(Dateien.Pdf(3), "rechnung.pdf");

        ergebnis.ShouldBe(new FormatErgebnis(ErkanntesFormat.Pdf, null, 3));
    }

    [Theory]
    [InlineData("factur-x.xml")]
    [InlineData("zugferd-invoice.xml")]
    [InlineData("ZUGFeRD-invoice.xml")]
    [InlineData("XRECHNUNG.xml")]
    public void ZUGFeRD_PDF_liefert_eingebettetes_XML(string anhang)
    {
        var ergebnis = Erkenne(Dateien.ZugferdPdf(anhang, 2), "rechnung.pdf");

        ergebnis.Format.ShouldBe(ErkanntesFormat.ZugferdPdf);
        ergebnis.Seitenanzahl.ShouldBe(2);
        Encoding.UTF8.GetString(ergebnis.ERechnungXml!).ShouldBe(Dateien.CiiXml);
    }

    [Fact]
    public void PDF_mit_fremdem_Anhang_bleibt_normales_PDF()
    {
        Erkenne(Dateien.ZugferdPdf("notiz.xml"), "rechnung.pdf").Format.ShouldBe(ErkanntesFormat.Pdf);
    }

    [Fact]
    public void CII_XML_mit_BOM_ist_XRechnung()
    {
        var inhalt = Dateien.Cii();

        var ergebnis = Erkenne(inhalt, "rechnung.xml");

        ergebnis.Format.ShouldBe(ErkanntesFormat.XRechnungXml);
        ergebnis.ERechnungXml.ShouldBe(inhalt);
        ergebnis.Seitenanzahl.ShouldBe(1);
    }

    [Fact]
    public void UBL_Rechnung_und_Gutschrift_sind_XRechnung()
    {
        Erkenne(Dateien.Ubl(), "ubl.xml").Format.ShouldBe(ErkanntesFormat.XRechnungXml);
        Erkenne(Dateien.UblGutschrift(), "gutschrift.xml").Format.ShouldBe(ErkanntesFormat.XRechnungXml);
    }

    [Fact]
    public void Anderes_XML_ist_unbekannt()
    {
        Erkenne(Dateien.AnderesXml(), "bestellung.xml").ShouldBe(FormatErgebnis.Unbekannt);
    }

    [Fact]
    public void PNG_und_JPG_sind_Bilder_mit_einer_Seite()
    {
        Erkenne(Dateien.Png(), "scan.png").ShouldBe(new FormatErgebnis(ErkanntesFormat.Bild, null, 1));
        Erkenne(Dateien.Jpg(), "scan.jpg").ShouldBe(new FormatErgebnis(ErkanntesFormat.Bild, null, 1));
    }

    [Fact]
    public void TIFF_zaehlt_Frames()
    {
        Erkenne(Dateien.Tiff(3), "scan.tif").ShouldBe(new FormatErgebnis(ErkanntesFormat.Bild, null, 3));
    }

    [Fact]
    public void Falsche_Endung_aendert_nichts()
    {
        Erkenne(Dateien.Pdf(), "rechnung.png").Format.ShouldBe(ErkanntesFormat.Pdf);
        Erkenne(Dateien.Png(), "scan.pdf").Format.ShouldBe(ErkanntesFormat.Bild);
        Erkenne(Dateien.Cii(), "rechnung.pdf").Format.ShouldBe(ErkanntesFormat.XRechnungXml);
    }

    [Fact]
    public void Text_mit_Endung_pdf_ist_unbekannt()
    {
        Erkenne(Dateien.TextAlsPdf(), "rechnung.pdf").ShouldBe(FormatErgebnis.Unbekannt);
    }

    [Fact]
    public void Beschaedigtes_PDF_ist_unbekannt()
    {
        Erkenne(Dateien.AbgeschnittenesPdf(), "kaputt.pdf").ShouldBe(FormatErgebnis.Unbekannt);
    }

    [Fact]
    public void Leere_Datei_ist_unbekannt()
    {
        Erkenne([], "leer.pdf").ShouldBe(FormatErgebnis.Unbekannt);
    }
}
