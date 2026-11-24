using System.Globalization;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.DocumentIntelligence;

/// <summary>
/// Schritt 3b: liest PDFs und Bilder ohne strukturiertes XML mit Document Intelligence aus. Achtet auf das
/// Monatslimit (Pause bei 100 %, Warnung ab <see cref="DiEinstellungen.WarnungAbProzent"/>) und meldet
/// vorübergehende Ausfälle als <see cref="SchrittAusgang.SpaeterErneut"/>.
/// </summary>
public sealed class DocumentIntelligenceSchritt(
    IDocumentIntelligenceClient client,
    DiVerbrauchsZaehler zaehler,
    IEinstellungen einstellungen,
    IEnumerable<IBenachrichtigungsDienst> benachrichtigungen,
    ILogger<DocumentIntelligenceSchritt> log) : IPipelineSchritt
{
    public const string Link = "/einstellungen/document-intelligence";
    public const string GrundLimit = "Monatslimit erreicht";
    public const string GrundNichtErreichbar = "Document Intelligence nicht erreichbar";

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public int Reihenfolge => SchrittReihenfolge.DocumentIntelligence;

    public string Name => "Document Intelligence";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        if (kontext.Format is not (ErkanntesFormat.Pdf or ErkanntesFormat.Bild) || kontext.Extraktion is not null)
        {
            return SchrittErgebnis.Weiter;
        }

        if (!client.IstKonfiguriert)
        {
            return SchrittErgebnis.Weiter; // Beleg wird von Hand erfasst
        }

        var di = await einstellungen.LesenAsync<DiEinstellungen>(ct);
        if (kontext.Beleg.Seitenanzahl > di.MaxSeitenJeBeleg)
        {
            log.LogInformation(
                "Beleg {Beleg} hat {Seiten} Seiten (max. {Max}); Document Intelligence entfällt",
                kontext.Beleg.Id, kontext.Beleg.Seitenanzahl, di.MaxSeitenJeBeleg);
            return SchrittErgebnis.Weiter;
        }

        var monat = await zaehler.MonatAsync(ct);
        if (!await zaehler.PasstNochAsync(Math.Max(kontext.Beleg.Seitenanzahl, 1), ct))
        {
            var verbrauch = await zaehler.LesenAsync(ct);
            await MeldenAsync(
                new NeueBenachrichtigung(
                    BenachrichtigungArt.DiLimitErreicht,
                    Schweregrad.Fehler,
                    "DI-Limit erreicht – Warteschlange pausiert",
                    $"{verbrauch.Seiten} von {verbrauch.Limit} Seiten im {Monatsname(monat)} verbraucht. "
                    + "Neue Belege warten bis zum Monatswechsel oder bis zur Limit-Erhöhung.",
                    Link,
                    NurAdmins: true,
                    EntdoppelungsSchluessel: $"di-limit-{monat:yyyy-MM}"),
                ct);
            return SchrittErgebnis.Pausiert(GrundLimit);
        }

        ExtraktionsErgebnis extraktion;
        try
        {
            await using var datei = File.OpenRead(kontext.DateiPfad);
            extraktion = await client.AnalysierenAsync(datei, ct);
        }
        catch (DiVoruebergehendNichtVerfuegbarException ex)
        {
            log.LogWarning("Document Intelligence für Beleg {Beleg} nicht erreichbar: {Fehler}", kontext.Beleg.Id, ex.Message);
            return SchrittErgebnis.SpaeterErneut(GrundNichtErreichbar);
        }

        kontext.Extraktion = extraktion;
        var gezaehlt = extraktion.Seiten > 0 ? extraktion.Seiten : Math.Max(kontext.Beleg.Seitenanzahl, 1);
        var nachher = await zaehler.ErhoehenAsync(gezaehlt, ct);
        var prozentVorher = DiVerbrauchsZaehler.Prozent(nachher - gezaehlt, di.Monatslimit);
        var prozent = DiVerbrauchsZaehler.Prozent(nachher, di.Monatslimit);
        if (prozentVorher < di.WarnungAbProzent && prozent >= di.WarnungAbProzent)
        {
            await MeldenAsync(
                new NeueBenachrichtigung(
                    BenachrichtigungArt.DiLimitWarnung,
                    Schweregrad.Warnung,
                    $"Document Intelligence bei {prozent} %",
                    $"{nachher} von {di.Monatslimit} Seiten im {Monatsname(monat)} verbraucht. "
                    + "Ab 100 % warten neue Belege in der Warteschlange.",
                    Link,
                    EntdoppelungsSchluessel: $"di-warnung-{monat:yyyy-MM}"),
                ct);
        }

        return SchrittErgebnis.Weiter;
    }

    private static string Monatsname(DateOnly monat) => monat.ToString("MMMM", Deutsch);

    private async Task MeldenAsync(NeueBenachrichtigung meldung, CancellationToken ct)
    {
        foreach (var dienst in benachrichtigungen)
        {
            await dienst.MeldenAsync(meldung, ct);
        }
    }
}
