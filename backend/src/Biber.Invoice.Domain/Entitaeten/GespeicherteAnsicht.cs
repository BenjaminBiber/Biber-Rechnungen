namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Gespeicherte Filter-Ansicht eines Benutzers; <see cref="Definition"/> ist JSON (<c>jsonb</c>).</summary>
public class GespeicherteAnsicht
{
    public Guid Id { get; set; }
    public Guid BenutzerId { get; set; }
    public string Name { get; set; } = "";
    public string Definition { get; set; } = "{}";
}
