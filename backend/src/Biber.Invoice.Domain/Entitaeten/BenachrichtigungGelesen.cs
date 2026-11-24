namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Ein Benutzer hat eine Benachrichtigung gelesen.</summary>
public class BenachrichtigungGelesen
{
    public Guid BenachrichtigungId { get; set; }
    public Guid BenutzerId { get; set; }
    public DateTimeOffset GelesenAm { get; set; }
}
