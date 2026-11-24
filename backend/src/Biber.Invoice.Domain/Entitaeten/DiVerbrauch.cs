namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Von Document Intelligence verarbeitete Seiten je Monat.</summary>
public class DiVerbrauch
{
    /// <summary>Erster Tag des Monats.</summary>
    public DateOnly Monat { get; set; }

    public int Seiten { get; set; }
}
