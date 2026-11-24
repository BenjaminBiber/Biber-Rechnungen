namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Ein Artikel eines Lieferanten, eindeutig über den normalisierten Namen.</summary>
public class Artikel
{
    public Guid Id { get; set; }
    public Guid KorrespondentId { get; set; }
    public string Name { get; set; } = "";
    public string NameNormalisiert { get; set; } = "";
    public string? Einheit { get; set; }
    public Guid? StammartikelId { get; set; }

    /// <summary>Wie viele Basiseinheiten des Stammartikels eine Einheit dieses Artikels enthält.</summary>
    public decimal? UmrechnungFaktor { get; set; }

    public Korrespondent? Korrespondent { get; set; }
    public Stammartikel? Stammartikel { get; set; }
}
