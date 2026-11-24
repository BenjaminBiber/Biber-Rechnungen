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
}
