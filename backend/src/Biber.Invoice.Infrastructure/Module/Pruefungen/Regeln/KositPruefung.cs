using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>9. Ergebnis der KoSIT-Validierung einer E-Rechnung aus der Pipeline.</summary>
public sealed class KositPruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.Kosit;

    public string Name => "KoSIT-Validierung";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        if (e.Beleg.ExtraktionsQuelle is not (ExtraktionsQuelle.XRechnung or ExtraktionsQuelle.ZUGFeRD))
        {
            return PruefBefundWert.NichtAnwendbar("Keine E-Rechnung (gescanntes PDF)");
        }

        if (e.Kosit is not { } kosit)
        {
            return PruefBefundWert.NichtAnwendbar("Nicht validiert");
        }

        if (kosit.Gueltig)
        {
            return PruefBefundWert.Ok("E-Rechnung gültig");
        }

        var meldungen = kosit.Meldungen.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).ToList();
        return PruefBefundWert.Fehler(meldungen.Count == 0 ? "KoSIT: E-Rechnung ungültig" : "KoSIT: " + string.Join(" · ", meldungen));
    }
}
