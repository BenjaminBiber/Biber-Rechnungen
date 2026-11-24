namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Schlagwort an Belegen.</summary>
public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Chip-Ton: <c>blau</c>, <c>orange</c>, <c>gruen</c>, <c>rot</c>, <c>grau</c>, <c>petrol</c> oder <c>lila</c>.</summary>
    public string Farbe { get; set; } = "grau";

    public bool IstInbox { get; set; }
    public bool IstZahlungsTag { get; set; }
    public bool IstSystem { get; set; }
    public Matching Matching { get; set; } = new();
}
