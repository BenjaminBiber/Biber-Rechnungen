using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Domain.Regeln;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Biber.Invoice.Infrastructure.Module.Belegpruefung;

/// <summary>
/// Sortiert vor Document Intelligence aus, was nach Dateiname oder Textebene des PDFs eindeutig kein Beleg ist
/// (<see cref="BelegErkenner"/>) – das spart Seiten vom DI-Monatslimit. Scans ohne Textebene prüft danach
/// <see cref="BelegpruefungNachAuslesenSchritt"/>.
/// </summary>
public sealed class BelegpruefungSchritt(ILogger<BelegpruefungSchritt> log) : IPipelineSchritt
{
    /// <summary>Für die Einschätzung reicht der Anfang; AGB und Rechnungen zeigen auf den ersten Seiten, was sie sind.</summary>
    public const int Seiten = 2;

    public int Reihenfolge => SchrittReihenfolge.Belegpruefung;

    public string Name => "Belegprüfung";

    public Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        if (!BelegpruefungRegeln.Gilt(kontext)
            || kontext.Format is not (ErkanntesFormat.Pdf or ErkanntesFormat.Bild)
            || kontext.Extraktion is not null)
        {
            return Task.FromResult(SchrittErgebnis.Weiter);
        }

        var text = kontext.Format == ErkanntesFormat.Pdf ? Textebene(kontext.DateiPfad) : null;
        var einschaetzung = BelegErkenner.Einschaetzen(kontext.Dateiname, text);
        if (einschaetzung.Urteil != BelegUrteil.KeinBeleg)
        {
            return Task.FromResult(SchrittErgebnis.Weiter);
        }

        log.LogInformation("Import {Vorgang} ({Datei}) aussortiert: {Grund}", kontext.Vorgang.Id, kontext.Dateiname, einschaetzung.Grund);
        return Task.FromResult(BelegpruefungRegeln.Aussortieren(einschaetzung.Grund));
    }

    /// <summary>Text der ersten Seiten; <c>null</c> bei Scans ohne Textebene oder unlesbarem PDF.</summary>
    private string? Textebene(string pfad)
    {
        try
        {
            using var dokument = PdfDocument.Open(pfad);
            var woerter = dokument.GetPages().Take(Seiten).SelectMany(seite => seite.GetWords()).Select(wort => wort.Text);
            var text = string.Join(' ', woerter);
            return text.Count(char.IsLetter) >= 20 ? text : null;
        }
        catch (Exception ex)
        {
            // PdfPig wirft bei beschädigten Dateien verschiedene Ausnahmetypen; dann entscheidet der Inhalt nach dem Auslesen.
            log.LogDebug(ex, "Textebene von {Pfad} nicht lesbar", pfad);
            return null;
        }
    }
}
