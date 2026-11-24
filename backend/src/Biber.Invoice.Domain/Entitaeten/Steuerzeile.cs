namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Steueraufschlüsselung eines Belegs je Steuersatz.</summary>
public class Steuerzeile
{
    public Guid Id { get; set; }
    public Guid BelegId { get; set; }

    /// <summary>Steuersatz in Prozent, z. B. 19.</summary>
    public decimal Satz { get; set; }

    public decimal Netto { get; set; }
    public decimal Steuer { get; set; }
}
