using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>
/// 5. Rechnungsangaben nach § 14 UStG. Bei Kleinbetragsrechnungen (Brutto bis <c>KleinbetragGrenze</c>, § 33 UStDV)
/// reicht der reduzierte Umfang; bei USt-Status „Kleinunternehmer“ entfällt die Prüfung.
/// </summary>
public sealed class Paragraph14Pruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.Paragraph14;

    public string Name => "Angaben nach § 14 UStG";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        if (e.Ust.Status == UstStatus.Kleinunternehmer)
        {
            return PruefBefundWert.NichtAnwendbar("Entfällt bei Kleinunternehmern (§ 19 UStG)");
        }

        var b = e.Beleg;
        var k = e.Korrespondent;
        var fehlend = new List<string>();
        if (string.IsNullOrWhiteSpace(k?.Name))
        {
            fehlend.Add("Name des Ausstellers");
        }

        if (string.IsNullOrWhiteSpace(k?.Anschrift.Strasse) || string.IsNullOrWhiteSpace(k.Anschrift.Ort))
        {
            fehlend.Add("Anschrift des Ausstellers");
        }

        if (b.Rechnungsdatum is null)
        {
            fehlend.Add("Rechnungsdatum");
        }

        if (b.Brutto is null)
        {
            fehlend.Add("Gesamtbetrag");
        }

        var hatSteuersatz = b.Steuerzeilen.Count > 0 || b.Positionen.Any(p => p.Steuersatz is not null);
        if (b.Brutto <= e.Einstellungen.KleinbetragGrenze)
        {
            if (!hatSteuersatz)
            {
                fehlend.Add("Steuersatz");
            }

            return fehlend.Count == 0
                ? PruefBefundWert.Ok("Kleinbetragsrechnung: Aussteller, Datum, Gesamtbetrag, Steuersatz vorhanden")
                : PruefBefundWert.Fehler(PruefFormat.Fehlt(fehlend));
        }

        // Die Positivliste nennt wie im Design nur, was über die Pflichtfelder hinausgeht.
        var vorhanden = new List<string>();
        Merken(!string.IsNullOrWhiteSpace(k?.UstIdNr) ? "USt-IdNr." : !string.IsNullOrWhiteSpace(k?.Steuernummer) ? "Steuernummer" : null, "USt-IdNr. oder Steuernummer");
        if (string.IsNullOrWhiteSpace(b.Rechnungsnummer))
        {
            fehlend.Add("Rechnungsnummer");
        }

        Merken(b.Leistungsdatum is not null ? "Leistungsdatum" : b.LeistungVon is not null ? "Leistungszeitraum" : null, "Leistungsdatum");
        if (e.Empfaenger is not null)
        {
            Merken(string.IsNullOrWhiteSpace(e.Empfaenger.Name) ? null : "Empfänger", "Empfänger");
        }

        Merken(b.Steuerzeilen.Count > 0 ? "Steuersatz" : null, "Aufschlüsselung nach Steuersätzen");

        return fehlend.Count == 0
            ? PruefBefundWert.Ok(string.Join(", ", vorhanden) + " vorhanden")
            : PruefBefundWert.Fehler(PruefFormat.Fehlt(fehlend));

        void Merken(string? gefunden, string fehltText)
        {
            if (gefunden is null)
            {
                fehlend.Add(fehltText);
            }
            else
            {
                vorhanden.Add(gefunden);
            }
        }
    }
}
