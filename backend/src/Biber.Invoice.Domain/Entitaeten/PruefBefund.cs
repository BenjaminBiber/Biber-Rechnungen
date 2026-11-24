using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Ergebnis einer Plausibilitätsprüfung an einem Beleg.</summary>
public class PruefBefund
{
    public Guid Id { get; set; }
    public Guid BelegId { get; set; }
    public PruefungArt Pruefung { get; set; }
    public PruefErgebnis Ergebnis { get; set; }
    public string Detail { get; set; } = "";
    public DateTimeOffset Zeitpunkt { get; set; }
}
