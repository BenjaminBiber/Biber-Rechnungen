using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>3. Korrespondent, Rechnungsdatum, Rechnungsnummer und Brutto sind erfasst.</summary>
public sealed class PflichtfelderPruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.Pflichtfelder;

    public string Name => "Pflichtfelder";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        var b = e.Beleg;
        var fehlend = new List<string>();
        if (b.KorrespondentId is null)
        {
            fehlend.Add("Korrespondent");
        }

        if (b.Rechnungsdatum is null)
        {
            fehlend.Add("Datum");
        }

        if (string.IsNullOrWhiteSpace(b.Rechnungsnummer))
        {
            fehlend.Add("Rechnungsnummer");
        }

        if (b.Brutto is null)
        {
            fehlend.Add("Gesamtbetrag");
        }

        return fehlend.Count == 0
            ? PruefBefundWert.Ok("Korrespondent, Datum, Rechnungsnummer und Gesamtbetrag vorhanden")
            : PruefBefundWert.Fehler(PruefFormat.Fehlt(fehlend));
    }
}
