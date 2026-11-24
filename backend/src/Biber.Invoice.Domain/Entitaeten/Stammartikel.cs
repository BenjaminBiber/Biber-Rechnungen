namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Lieferantenübergreifender Artikel für Preisvergleiche.</summary>
public class Stammartikel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Basiseinheit { get; set; } = "";

    /// <summary>Bezugsmenge des Grundpreises, z. B. 100 für „je 100 Blatt“.</summary>
    public decimal GrundpreisMenge { get; set; } = 1;
}
