namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Eine Einstellung der Anwendung; <see cref="Wert"/> ist JSON (<c>jsonb</c>).</summary>
public class Einstellung
{
    public string Schluessel { get; set; } = "";
    public string Wert { get; set; } = "null";
    public DateTimeOffset GeaendertAm { get; set; }
}
