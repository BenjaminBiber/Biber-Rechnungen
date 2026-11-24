using System.Text;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.ERechnung;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class ERechnungSchrittTests
{
    private static readonly string Testdateien = Path.Combine(AppContext.BaseDirectory, "Testdateien", "erechnung");
    private static readonly byte[] KaputtesXml = Encoding.UTF8.GetBytes("<rsm:CrossIndustryInvoice");

    private static ERechnungSchritt Schritt(IERechnungParser? parser = null) =>
        new(parser ?? new ERechnungParser(), NullLogger<ERechnungSchritt>.Instance);

    private static VerarbeitungsKontext Kontext(ErkanntesFormat format, byte[]? xml = null, string? pfad = null) => new()
    {
        Vorgang = new ImportVorgang { Id = Guid.NewGuid() },
        Beleg = new Beleg(),
        DateiPfad = pfad ?? Path.Combine(Testdateien, "gibt-es-nicht.pdf"),
        Dateiname = "beleg.pdf",
        Format = format,
        ERechnungXml = xml,
    };

    [Fact]
    public void Reihenfolge_und_Name()
    {
        var schritt = Schritt();
        schritt.Reihenfolge.ShouldBe(SchrittReihenfolge.ERechnung);
        schritt.Name.ShouldBe("E-Rechnung lesen");
    }

    [Theory]
    [InlineData(ErkanntesFormat.Pdf)]
    [InlineData(ErkanntesFormat.Bild)]
    [InlineData(ErkanntesFormat.Unbekannt)]
    public async Task PDF_oder_Bild_ohne_XML_wird_uebersprungen(ErkanntesFormat format)
    {
        var parser = Substitute.For<IERechnungParser>();
        var kontext = Kontext(format);

        var ergebnis = await Schritt(parser).AusfuehrenAsync(kontext, CancellationToken.None);

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        kontext.Extraktion.ShouldBeNull();
        kontext.Format.ShouldBe(format);
        parser.DidNotReceiveWithAnyArgs().Parsen(default!);
    }

    [Fact]
    public async Task Eingebettetes_XML_setzt_Extraktion_mit_Quelle_ZUGFeRD()
    {
        var xml = await File.ReadAllBytesAsync(Path.Combine(Testdateien, "zugferd-en16931.xml"));
        var kontext = Kontext(ErkanntesFormat.ZugferdPdf, xml);

        var ergebnis = await Schritt().AusfuehrenAsync(kontext, CancellationToken.None);

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        kontext.Format.ShouldBe(ErkanntesFormat.ZugferdPdf);
        var e = kontext.Extraktion.ShouldNotBeNull();
        e.Quelle.ShouldBe(ExtraktionsQuelle.ZUGFeRD);
        e.Rechnungsnummer.ShouldBe("ZF-2026-0004");
        e.Brutto.ShouldBe(95.20m);
    }

    [Fact]
    public async Task Kaputtes_eingebettetes_XML_stuft_auf_Pdf_zurueck()
    {
        var kontext = Kontext(ErkanntesFormat.ZugferdPdf, KaputtesXml);

        var ergebnis = await Schritt().AusfuehrenAsync(kontext, CancellationToken.None);

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        kontext.Format.ShouldBe(ErkanntesFormat.Pdf);
        kontext.Extraktion.ShouldBeNull();
    }

    [Fact]
    public async Task ZUGFeRD_PDF_ohne_XML_stuft_auf_Pdf_zurueck()
    {
        var kontext = Kontext(ErkanntesFormat.ZugferdPdf);

        await Schritt().AusfuehrenAsync(kontext, CancellationToken.None);

        kontext.Format.ShouldBe(ErkanntesFormat.Pdf);
        kontext.Extraktion.ShouldBeNull();
    }

    [Fact]
    public async Task XRechnung_ohne_vorgelesenes_XML_liest_die_Datei()
    {
        var kontext = Kontext(ErkanntesFormat.XRechnungXml, pfad: Path.Combine(Testdateien, "xrechnung-ubl-invoice.xml"));

        await Schritt().AusfuehrenAsync(kontext, CancellationToken.None);

        var e = kontext.Extraktion.ShouldNotBeNull();
        e.Quelle.ShouldBe(ExtraktionsQuelle.XRechnung);
        e.Rechnungsnummer.ShouldBe("RE-2026-0002");
        kontext.Format.ShouldBe(ErkanntesFormat.XRechnungXml);
    }

    [Fact]
    public async Task Kaputte_XRechnung_laesst_Extraktion_leer_und_Format_unveraendert()
    {
        var kontext = Kontext(ErkanntesFormat.XRechnungXml, KaputtesXml);
        kontext.Extraktion = new ExtraktionsErgebnis { Rechnungsnummer = "aus einem früheren Lauf" };

        var ergebnis = await Schritt().AusfuehrenAsync(kontext, CancellationToken.None);

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        kontext.Format.ShouldBe(ErkanntesFormat.XRechnungXml);
        kontext.Extraktion.ShouldBeNull();
    }
}
