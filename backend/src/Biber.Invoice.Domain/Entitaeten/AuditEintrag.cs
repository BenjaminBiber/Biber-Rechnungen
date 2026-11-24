namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Eintrag im Änderungsprotokoll. Die Tabelle ist append-only (DB-Trigger, B7).</summary>
public class AuditEintrag
{
    public long Id { get; set; }
    public DateTimeOffset Zeitpunkt { get; set; }
    public Guid? BenutzerId { get; set; }

    /// <summary>Anzeigename des Benutzers, „System“ ohne Benutzer.</summary>
    public string BenutzerName { get; set; } = "System";

    public string Entitaet { get; set; } = "";
    public string EntitaetId { get; set; } = "";

    /// <summary>Beleg, zu dem die Änderung gehört – zum schnellen Lesen der Beleg-Historie.</summary>
    public Guid? BelegId { get; set; }

    public string Aktion { get; set; } = "";
    public string? Feld { get; set; }
    public string? AlterWert { get; set; }
    public string? NeuerWert { get; set; }
}
