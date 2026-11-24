using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Infrastructure.Module.Formaterkennung;

/// <summary>Bestimmt das Dateiformat, liest das E-Rechnungs-XML aus und lehnt unbekannte Formate ab.</summary>
public sealed class FormaterkennungSchritt(IFormatErkenner erkenner) : IPipelineSchritt
{
    public const string TextUnbekannt = "Datei ist weder PDF, E-Rechnung noch Bild.";

    public int Reihenfolge => SchrittReihenfolge.Formaterkennung;

    public string Name => "Format";

    public Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        FormatErgebnis ergebnis;
        using (var datei = File.OpenRead(kontext.DateiPfad))
        {
            ergebnis = erkenner.Erkennen(datei, kontext.Dateiname);
        }

        kontext.Format = ergebnis.Format;
        kontext.ERechnungXml = ergebnis.ERechnungXml;
        kontext.Beleg.Seitenanzahl = ergebnis.Seitenanzahl;

        if (ergebnis.Format == ErkanntesFormat.Unbekannt)
        {
            kontext.Beleg.Status = BelegStatus.Fehler;
            return Task.FromResult(SchrittErgebnis.Beendet(ImportStatus.Abgelehnt, TextUnbekannt));
        }

        return Task.FromResult(SchrittErgebnis.Weiter);
    }
}
