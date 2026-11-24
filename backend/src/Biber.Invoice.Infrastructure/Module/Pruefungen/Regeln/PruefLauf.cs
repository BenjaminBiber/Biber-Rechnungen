using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>Führt die eingeschalteten Prüfungen aus; eine abgeschaltete Prüfung liefert keinen Befund.</summary>
public static class PruefLauf
{
    /// <summary>Befunde in der Reihenfolge von <see cref="PruefungArt"/>.</summary>
    public static IReadOnlyList<(PruefungArt Art, PruefBefundWert Wert)> Ausfuehren(PruefEingabe eingabe, IEnumerable<IPruefung> pruefungen) =>
    [
        .. pruefungen
            .Where(p => eingabe.Einstellungen.Aktiv.GetValueOrDefault(p.Art, true))
            .OrderBy(p => p.Art)
            .Select(p => (p.Art, p.Pruefen(eingabe))),
    ];
}
