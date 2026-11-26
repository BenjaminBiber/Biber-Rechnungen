namespace Biber.Invoice.Domain.Enums;

/// <summary>Ergebnis der Online-Prüfung einer USt-IdNr. über VIES.</summary>
public enum UstIdStatus
{
    /// <summary>Noch nicht geprüft oder die USt-IdNr. hat sich seit der letzten Prüfung geändert.</summary>
    Ungeprueft,
    Gueltig,
    Ungueltig,

    /// <summary>VIES oder der Dienst des Mitgliedstaats war nicht erreichbar; wird später wiederholt.</summary>
    NichtErreichbar,

    /// <summary>Keine USt-IdNr. eines EU-Mitgliedstaats (z. B. CH, GB) – VIES kennt sie nicht.</summary>
    NichtPruefbar,
}
