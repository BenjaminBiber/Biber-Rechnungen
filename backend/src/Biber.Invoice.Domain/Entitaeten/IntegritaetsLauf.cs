namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Ein Lauf der Integritätsprüfung über die Ablage.</summary>
public class IntegritaetsLauf
{
    public Guid Id { get; set; }
    public DateTimeOffset GestartetAm { get; set; }
    public DateTimeOffset? BeendetAm { get; set; }
    public int AnzahlDateien { get; set; }
    public int AnzahlAbweichungen { get; set; }

    /// <summary>Pfade der abweichenden Dateien, als <c>jsonb</c> gespeichert.</summary>
    public List<string> Abweichungen { get; set; } = [];
}
