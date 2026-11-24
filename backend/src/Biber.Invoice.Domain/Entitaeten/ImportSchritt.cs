namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Ein Pipeline-Schritt eines <see cref="ImportVorgang"/> (Teil des <c>jsonb</c>-Felds <c>Schritte</c>).</summary>
public class ImportSchritt
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
}
