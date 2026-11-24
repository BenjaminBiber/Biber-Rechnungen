using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.ERechnung;

/// <summary>
/// Liest das XML einer E-Rechnung (Q14: maßgeblich, dann kein Document Intelligence). Ist eingebettetes XML
/// unlesbar, wird der Beleg zur einfachen PDF; ist eine reine XML-Datei unlesbar, bleibt die Extraktion leer.
/// </summary>
public sealed class ERechnungSchritt(IERechnungParser parser, ILogger<ERechnungSchritt> log) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.ERechnung;

    public string Name => "E-Rechnung lesen";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        if (kontext.Format is not (ErkanntesFormat.XRechnungXml or ErkanntesFormat.ZugferdPdf))
        {
            return SchrittErgebnis.Weiter;
        }

        var xml = kontext.ERechnungXml;
        if (xml is null && kontext.Format == ErkanntesFormat.XRechnungXml)
        {
            xml = await File.ReadAllBytesAsync(kontext.DateiPfad, ct);
        }

        try
        {
            if (xml is null)
            {
                throw new ERechnungFormatException("Kein eingebettetes XML vorhanden.");
            }

            var ergebnis = parser.Parsen(xml);
            kontext.Extraktion = kontext.Format == ErkanntesFormat.ZugferdPdf
                ? ergebnis with { Quelle = ExtraktionsQuelle.ZUGFeRD }
                : ergebnis;
        }
        catch (ERechnungFormatException ex)
        {
            log.LogWarning(
                ex, "E-Rechnung von Vorgang {VorgangId} nicht lesbar (Format {Format})", kontext.Vorgang.Id, kontext.Format);
            kontext.Extraktion = null;
            if (kontext.Format == ErkanntesFormat.ZugferdPdf)
            {
                kontext.Format = ErkanntesFormat.Pdf;
            }
        }

        return SchrittErgebnis.Weiter;
    }
}
