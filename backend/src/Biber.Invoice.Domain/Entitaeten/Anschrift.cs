namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Postanschrift eines Korrespondenten (Owned-Typ).</summary>
public class Anschrift
{
    public string? Strasse { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }
    public string? Land { get; set; }
}
