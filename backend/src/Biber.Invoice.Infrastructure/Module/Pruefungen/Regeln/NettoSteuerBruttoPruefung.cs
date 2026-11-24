using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>2. Je Steuersatz ergibt Netto × Satz die Steuer, und Netto plus Steuer aller Sätze ergibt das Brutto.</summary>
public sealed class NettoSteuerBruttoPruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.NettoSteuerBrutto;

    public string Name => "Netto + USt = Brutto je Steuersatz";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        var zeilen = e.Beleg.Steuerzeilen.OrderByDescending(z => z.Satz).ToList();
        if (zeilen.Count == 0)
        {
            return PruefBefundWert.NichtAnwendbar("Keine Aufschlüsselung nach Steuersätzen");
        }

        var toleranz = e.Einstellungen.SummenToleranz;
        var fehler = new List<string>();
        foreach (var zeile in zeilen)
        {
            var erwartet = Math.Round(zeile.Netto * zeile.Satz / 100m, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(erwartet - zeile.Steuer) > toleranz)
            {
                fehler.Add($"{PruefFormat.Satz(zeile.Satz)}: Steuer {PruefFormat.Euro(zeile.Steuer)} statt {PruefFormat.Euro(erwartet)}");
            }
        }

        var summe = zeilen.Sum(z => z.Netto + z.Steuer);
        if (e.Beleg.Brutto is { } brutto && Math.Abs(summe - brutto) > toleranz)
        {
            fehler.Add($"Netto + USt {PruefFormat.Euro(summe)} statt Brutto {PruefFormat.Euro(brutto)}");
        }

        return fehler.Count > 0
            ? PruefBefundWert.Fehler(string.Join(" · ", fehler))
            : PruefBefundWert.Ok(string.Join(" · ", zeilen.Select(z =>
                $"{PruefFormat.Satz(z.Satz)}: {PruefFormat.Euro(z.Netto)} + {PruefFormat.Euro(z.Steuer)} = {PruefFormat.Euro(z.Netto + z.Steuer)}")));
    }
}
