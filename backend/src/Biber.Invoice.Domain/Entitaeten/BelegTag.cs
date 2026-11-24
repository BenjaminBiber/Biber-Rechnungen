namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Ein Tag an einem Beleg.</summary>
public class BelegTag
{
    public Guid BelegId { get; set; }
    public Guid TagId { get; set; }
    public DateTimeOffset GesetztAm { get; set; }
    public Guid? GesetztVonId { get; set; }

    public Tag? Tag { get; set; }
}
