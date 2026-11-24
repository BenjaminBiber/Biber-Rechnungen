using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Formaterkennung;
using Dateien = Biber.Invoice.UnitTests.Testdateien.Formate.Testdateien;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class FormaterkennungSchrittTests : IDisposable
{
    private readonly string _pfad = Path.GetTempFileName();
    private readonly FormaterkennungSchritt _schritt = new(new FormatErkenner());

    public void Dispose() => File.Delete(_pfad);

    private VerarbeitungsKontext Kontext(byte[] inhalt)
    {
        File.WriteAllBytes(_pfad, inhalt);
        return new VerarbeitungsKontext
        {
            Vorgang = new ImportVorgang(),
            Beleg = new Beleg { Status = BelegStatus.InVerarbeitung },
            DateiPfad = _pfad,
            Dateiname = "rechnung.pdf",
        };
    }

    [Fact]
    public void Steht_an_Position_Formaterkennung()
    {
        _schritt.Reihenfolge.ShouldBe(SchrittReihenfolge.Formaterkennung);
        _schritt.Name.ShouldBe("Format");
    }

    [Fact]
    public async Task Unbekanntes_Format_endet_mit_Abgelehnt()
    {
        var kontext = Kontext(Dateien.TextAlsPdf());

        var ergebnis = await _schritt.AusfuehrenAsync(kontext, CancellationToken.None);

        ergebnis.ShouldBe(SchrittErgebnis.Beendet(ImportStatus.Abgelehnt, FormaterkennungSchritt.TextUnbekannt));
        kontext.Beleg.Status.ShouldBe(BelegStatus.Fehler);
        kontext.Format.ShouldBe(ErkanntesFormat.Unbekannt);
    }

    [Fact]
    public async Task ZUGFeRD_setzt_XML_und_Seitenanzahl()
    {
        var kontext = Kontext(Dateien.ZugferdPdf(seiten: 2));

        var ergebnis = await _schritt.AusfuehrenAsync(kontext, CancellationToken.None);

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        kontext.Format.ShouldBe(ErkanntesFormat.ZugferdPdf);
        kontext.ERechnungXml.ShouldNotBeNull();
        kontext.Beleg.Seitenanzahl.ShouldBe(2);
        kontext.Beleg.Status.ShouldBe(BelegStatus.InVerarbeitung);
    }
}
