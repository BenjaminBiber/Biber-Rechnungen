using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Eine abgelegte Datei eines Belegs (Original, eingebettetes XML, Visualisierung, Vorschau).</summary>
public class BelegDatei
{
    public Guid Id { get; set; }
    public Guid BelegId { get; set; }
    public DateiRolle Rolle { get; set; }
    public string Pfad { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Groesse { get; set; }
    public string MimeType { get; set; } = "";
    public DateTimeOffset AbgelegtAm { get; set; }
    public DateTimeOffset? LetztePruefungAm { get; set; }
    public bool? PruefErgebnis { get; set; }
}
