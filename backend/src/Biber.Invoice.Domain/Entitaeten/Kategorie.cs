namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Kostenkategorie, optional einer EÜR-Zeile zugeordnet.</summary>
public class Kategorie
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Farbe als Hex-Wert, z. B. <c>#1F6FEB</c>.</summary>
    public string Farbe { get; set; } = "";

    public string? EuerZeile { get; set; }
    public Matching Matching { get; set; } = new();
}
