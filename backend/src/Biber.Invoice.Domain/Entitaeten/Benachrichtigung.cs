using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Benachrichtigung in der Glocke der Oberfläche.</summary>
public class Benachrichtigung
{
    public Guid Id { get; set; }
    public BenachrichtigungArt Art { get; set; }
    public Schweregrad Schweregrad { get; set; }
    public string Titel { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Link { get; set; }
    public bool NurAdmins { get; set; }

    /// <summary>Verhindert doppelte Benachrichtigungen zum selben Anlass; eindeutig, wenn gesetzt.</summary>
    public string? EntdoppelungsSchluessel { get; set; }

    public DateTimeOffset ErstelltAm { get; set; }

    public List<BenachrichtigungGelesen> Gelesen { get; set; } = [];
}
