using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>
/// Überträgt <see cref="VerarbeitungsKontext.Extraktion"/> in den Beleg. Steuerzeilen und Positionen werden
/// ersetzt, damit eine Wiederholung nichts doppelt anlegt. Ohne Extraktion: Erfassung von Hand.
/// </summary>
public sealed class ExtraktionUebernehmenSchritt(TimeProvider zeit) : IPipelineSchritt
{
    public const int TitelLaenge = 80;

    public int Reihenfolge => SchrittReihenfolge.ExtraktionUebernehmen;

    public string Name => "Daten übernehmen";

    public Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        if (kontext.Extraktion is not { } e)
        {
            beleg.ExtraktionsQuelle = ExtraktionsQuelle.Manuell;
            return Task.FromResult(SchrittErgebnis.Weiter);
        }

        beleg.Belegart = e.Belegart;
        beleg.Rechnungsnummer = e.Rechnungsnummer;
        beleg.Rechnungsdatum = e.Rechnungsdatum;
        beleg.Leistungsdatum = e.Leistungsdatum;
        beleg.LeistungVon = e.LeistungVon;
        beleg.LeistungBis = e.LeistungBis;
        beleg.Faelligkeitsdatum = e.Faelligkeitsdatum;
        beleg.Waehrung = e.Waehrung;
        beleg.Netto = e.Netto;
        beleg.Steuer = e.Steuer;
        beleg.Brutto = e.Brutto;
        beleg.Volltext = e.Volltext;
        // Ohne Seitenzahl aus der Extraktion bleibt die der Formaterkennung (z. B. TIFF-Seiten) stehen.
        if (e.Seiten > 0)
        {
            beleg.Seitenanzahl = e.Seiten;
        }

        beleg.ExtraktionsQuelle = e.Quelle;
        beleg.DiKonfidenz = e.Konfidenz;
        beleg.ExtrahiertAm = zeit.GetUtcNow();

        // Ids bleiben leer: EF vergibt sie beim Einfügen und erkennt die Einträge so als neu.
        beleg.Steuerzeilen.Clear();
        beleg.Steuerzeilen.AddRange(e.Steuerzeilen.Select(s => new Steuerzeile
        {
            BelegId = beleg.Id,
            Satz = s.Satz,
            Netto = s.Netto,
            Steuer = s.Steuer,
        }));

        beleg.Positionen.Clear();
        beleg.Positionen.AddRange(e.Positionen.OrderBy(p => p.Nr).Select(p => new Position
        {
            BelegId = beleg.Id,
            Nr = p.Nr,
            Text = p.Text,
            Menge = p.Menge,
            Einheit = p.Einheit,
            Einzelpreis = p.Einzelpreis,
            Gesamt = p.Gesamt,
            Steuersatz = p.Steuersatz,
        }));

        if (beleg.Positionen.Count > 0)
        {
            var text = beleg.Positionen[0].Text.Trim();
            beleg.Titel = text.Length > TitelLaenge ? text[..TitelLaenge] : text;
        }

        return Task.FromResult(SchrittErgebnis.Weiter);
    }
}
