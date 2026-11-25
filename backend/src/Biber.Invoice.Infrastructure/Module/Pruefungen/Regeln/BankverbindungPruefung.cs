using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Korrespondenten;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>
/// 10. Die IBAN auf einer Eingangsrechnung ist beim Korrespondenten hinterlegt. Eine abweichende IBAN kann auf eine
/// gefälschte Rechnung hindeuten; sie wird erst mit dem Bestätigen des Belegs beim Korrespondenten übernommen.
/// </summary>
public sealed class BankverbindungPruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.Bankverbindung;

    public string Name => "Bankverbindung bekannt";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        if (e.Beleg.Richtung != Richtung.Eingang)
        {
            return PruefBefundWert.NichtAnwendbar("Nur für Eingangsbelege");
        }

        if (e.Beleg.Bankverbindungen.Count == 0)
        {
            return PruefBefundWert.NichtAnwendbar("Keine IBAN auf dem Beleg erkannt");
        }

        if (e.Korrespondent is not { } k)
        {
            return PruefBefundWert.NichtAnwendbar("Kein Korrespondent zugeordnet");
        }

        if (k.Bankverbindungen.Count == 0)
        {
            return PruefBefundWert.NichtAnwendbar("Beim Korrespondenten ist noch keine IBAN hinterlegt");
        }

        var unbekannt = BankverbindungenAbgleich.Unbekannte(k, e.Beleg.Bankverbindungen);
        if (unbekannt.Count == 0)
        {
            return PruefBefundWert.Ok("IBAN ist beim Korrespondenten hinterlegt");
        }

        var neu = string.Join(", ", unbekannt.Select(PruefFormat.Iban));
        var bekannt = string.Join(", ", k.Bankverbindungen.Select(b => PruefFormat.Iban(b.Iban)));
        return unbekannt.All(KorrespondentNormalisierung.IstIban)
            ? PruefBefundWert.Fehler($"Abweichende IBAN {neu} – bisher bekannt: {bekannt}")
            : PruefBefundWert.Fehler($"IBAN {neu} hat keine gültige Prüfziffer (vermutlich falsch gelesen) – bisher bekannt: {bekannt}");
    }
}
