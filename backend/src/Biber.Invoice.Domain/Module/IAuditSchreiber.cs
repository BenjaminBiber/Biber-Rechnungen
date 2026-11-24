namespace Biber.Invoice.Domain.Module;

/// <summary>
/// Notiert Ereignisse ohne Datenänderung (z. B. Export, Bestätigung mit Begründung) im Änderungsprotokoll.
/// Die Einträge werden beim nächsten <c>SaveChanges</c> in derselben Transaktion mitgeschrieben.
/// </summary>
public interface IAuditSchreiber
{
    void Notieren(string entitaet, string entitaetId, Guid? belegId, string aktion, string? text = null);
}
