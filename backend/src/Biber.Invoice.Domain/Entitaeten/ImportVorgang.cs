using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Ein Importversuch einer Datei (Upload, Ordner, Mail, MCP).</summary>
public class ImportVorgang
{
    public Guid Id { get; set; }
    public ImportQuelle Quelle { get; set; }
    public ImportStatus Status { get; set; }
    public string Dateiname { get; set; } = "";
    public long Groesse { get; set; }
    public string? Sha256 { get; set; }
    public string? StagingPfad { get; set; }
    public string? HerkunftPfad { get; set; }
    public string? MailMessageId { get; set; }
    public string? MailAbsender { get; set; }
    public string? MailBetreff { get; set; }
    public Guid? BenutzerId { get; set; }
    public Guid? ApiTokenId { get; set; }
    public Guid? BelegId { get; set; }
    public string? Fehler { get; set; }
    public string? Ergebnis { get; set; }
    public int Versuche { get; set; }
    public DateTimeOffset? NaechsterVersuchAm { get; set; }

    /// <summary>Ein Benutzer hat die aussortierte Datei als Beleg übernommen; die Belegprüfung entfällt.</summary>
    public bool AlsBelegBestaetigt { get; set; }

    /// <summary>Fortschritt der Pipeline, als <c>jsonb</c> gespeichert.</summary>
    public List<ImportSchritt> Schritte { get; set; } = [];

    public DateTimeOffset ErstelltAm { get; set; }
    public DateTimeOffset? BeendetAm { get; set; }
}
