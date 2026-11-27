using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Domain.Regeln;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Belegpruefung;

/// <summary>
/// Prüft nach Document Intelligence den ausgelesenen Text (auch von Scans und Bildern) mit dem <see cref="BelegErkenner"/>.
/// Bleibt er unklar und hat DI weder Betrag noch Rechnungsnummer noch Aussteller gefunden, ist die Datei kein Beleg.
/// </summary>
public sealed class BelegpruefungNachAuslesenSchritt(ILogger<BelegpruefungNachAuslesenSchritt> log) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.BelegpruefungNachAuslesen;

    public string Name => "Inhalt prüfen";

    public Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        if (!BelegpruefungRegeln.Gilt(kontext) || kontext.Extraktion is not { Quelle: ExtraktionsQuelle.DocumentIntelligence } extraktion)
        {
            return Task.FromResult(SchrittErgebnis.Weiter);
        }

        var einschaetzung = BelegErkenner.Einschaetzen(kontext.Dateiname, extraktion.Volltext);
        var grund = einschaetzung.Urteil switch
        {
            BelegUrteil.KeinBeleg => einschaetzung.Grund,
            BelegUrteil.Unklar when OhneDaten(extraktion) => BelegpruefungRegeln.GrundOhneDaten,
            _ => null,
        };
        if (grund is null)
        {
            return Task.FromResult(SchrittErgebnis.Weiter);
        }

        log.LogInformation("Import {Vorgang} ({Datei}) aussortiert: {Grund}", kontext.Vorgang.Id, kontext.Dateiname, grund);
        return Task.FromResult(BelegpruefungRegeln.Aussortieren(grund));
    }

    private static bool OhneDaten(ExtraktionsErgebnis e) =>
        e.Brutto is null && string.IsNullOrWhiteSpace(e.Rechnungsnummer) && string.IsNullOrWhiteSpace(e.Aussteller?.Name);
}
