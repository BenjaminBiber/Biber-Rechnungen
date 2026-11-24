using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>1. Die Summe der Positionen entspricht dem Netto, ersatzweise dem Brutto (Positionen mit Bruttopreisen).</summary>
public sealed class PositionssummePruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.Positionssumme;

    public string Name => "Summe der Positionen = Netto";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        var betraege = e.Beleg.Positionen.Where(p => p.Gesamt is not null).Select(p => p.Gesamt!.Value).ToList();
        if (betraege.Count == 0)
        {
            return PruefBefundWert.NichtAnwendbar("Keine Positionen mit Betrag");
        }

        var (netto, brutto) = (e.Beleg.Netto, e.Beleg.Brutto);
        if (netto is null && brutto is null)
        {
            return PruefBefundWert.NichtAnwendbar("Weder Netto noch Brutto erfasst");
        }

        var summe = betraege.Sum();
        var toleranz = e.Einstellungen.SummenToleranz;
        if (netto is { } n && Math.Abs(summe - n) <= toleranz)
        {
            return PruefBefundWert.Ok($"Positionen {PruefFormat.Euro(summe)} = Netto {PruefFormat.Euro(n)}");
        }

        if (brutto is { } b && Math.Abs(summe - b) <= toleranz)
        {
            return PruefBefundWert.Ok($"Positionen {PruefFormat.Euro(summe)} = Brutto {PruefFormat.Euro(b)}");
        }

        var soll = netto ?? brutto!.Value;
        return PruefBefundWert.Fehler(
            $"Positionen {PruefFormat.Euro(summe)} statt {PruefFormat.Euro(soll)} · "
            + $"Differenz {PruefFormat.Euro(Math.Abs(summe - soll))}, Toleranz {PruefFormat.Euro(toleranz)}");
    }
}
