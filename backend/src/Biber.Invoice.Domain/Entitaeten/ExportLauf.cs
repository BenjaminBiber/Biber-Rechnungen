namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Ein erzeugter Export (z. B. für den Steuerberater).</summary>
public class ExportLauf
{
    public Guid Id { get; set; }
    public DateTimeOffset ErstelltAm { get; set; }
    public Guid BenutzerId { get; set; }
    public string BenutzerName { get; set; } = "";
    public DateOnly Von { get; set; }
    public DateOnly Bis { get; set; }
    public string Bezeichnung { get; set; } = "";

    /// <summary>Inhalt in Worten, z. B. „Belegliste (xlsx), Summen, Vorsteuer, ZIP“.</summary>
    public string Inhalt { get; set; } = "";

    public int AnzahlBelege { get; set; }
    public string Dateipfad { get; set; } = "";
    public string Dateiname { get; set; } = "";
}
