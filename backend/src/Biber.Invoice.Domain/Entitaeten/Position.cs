namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Eine Rechnungsposition.</summary>
public class Position
{
    public Guid Id { get; set; }
    public Guid BelegId { get; set; }
    public int Nr { get; set; }
    public string Text { get; set; } = "";
    public string? TextNormalisiert { get; set; }
    public decimal? Menge { get; set; }
    public string? Einheit { get; set; }
    public decimal? Einzelpreis { get; set; }
    public decimal? Gesamt { get; set; }
    public decimal? Steuersatz { get; set; }
    public Guid? ArtikelId { get; set; }
    public decimal? Grundpreis { get; set; }
    public string? GrundpreisEinheit { get; set; }

    public Artikel? Artikel { get; set; }
}
