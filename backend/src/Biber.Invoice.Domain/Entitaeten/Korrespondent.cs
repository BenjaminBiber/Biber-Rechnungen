using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Lieferant oder Kunde.</summary>
public class Korrespondent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Aliase { get; set; } = [];
    public string? UstIdNr { get; set; }
    public string? Steuernummer { get; set; }
    public List<string> Ibans { get; set; } = [];
    public Anschrift Anschrift { get; set; } = new();
    public string? Website { get; set; }
    public string? Domain { get; set; }
    public string? Email { get; set; }
    public string? LogoPfad { get; set; }
    public LogoQuelle LogoQuelle { get; set; }
    public Guid? DefaultKategorieId { get; set; }
    public bool IstLieferant { get; set; }
    public bool IstKunde { get; set; }
    public Matching Matching { get; set; } = new();
    public DateTimeOffset ErstelltAm { get; set; }

    /// <summary><c>false</c> = „neu“, noch von keinem Benutzer geprüft.</summary>
    public bool Geprueft { get; set; }

    public Kategorie? DefaultKategorie { get; set; }
}
