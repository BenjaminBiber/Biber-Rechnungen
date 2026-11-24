namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Trainiertes ML.NET-Modell für die Auto-Zuordnung.</summary>
public class MatchingModell
{
    public Guid Id { get; set; }

    /// <summary><c>Tag</c>, <c>Kategorie</c> oder <c>Korrespondent</c>.</summary>
    public string Ziel { get; set; } = "";

    public byte[] Daten { get; set; } = [];
    public DateTimeOffset TrainiertAm { get; set; }
    public int AnzahlBeispiele { get; set; }
    public double? Guete { get; set; }
}
