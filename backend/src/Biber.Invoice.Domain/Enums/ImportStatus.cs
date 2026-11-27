namespace Biber.Invoice.Domain.Enums;

public enum ImportStatus
{
    Angenommen,
    InVerarbeitung,
    Wartet,
    Pausiert,
    Fertig,
    Dublette,
    OhneBeleg,
    Abgelehnt,
    Fehler,

    /// <summary>Kein Beleg (z. B. AGB neben der Rechnung): nichts abgelegt, die Datei bleibt zum Übernehmen im Staging.</summary>
    Aussortiert,
}
