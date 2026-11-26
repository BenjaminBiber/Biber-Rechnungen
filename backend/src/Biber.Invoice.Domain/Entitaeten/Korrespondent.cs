using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Lieferant oder Kunde.</summary>
public class Korrespondent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Aliase { get; set; } = [];
    public string? UstIdNr { get; set; }

    /// <summary>Ergebnis der letzten VIES-Prüfung; gilt nur, solange <see cref="UstIdGeprueftNummer"/> = <see cref="UstIdNr"/>.</summary>
    public UstIdStatus UstIdStatus { get; set; }

    public DateTimeOffset? UstIdGeprueftAm { get; set; }

    /// <summary>Die USt-IdNr., auf die sich <see cref="UstIdStatus"/> bezieht.</summary>
    public string? UstIdGeprueftNummer { get; set; }

    /// <summary>Name laut VIES; manche Staaten (z. B. DE) geben ihn nicht heraus.</summary>
    public string? UstIdAmtlicherName { get; set; }

    public string? UstIdAmtlicheAnschrift { get; set; }
    public string? Steuernummer { get; set; }
    public List<Bankverbindung> Bankverbindungen { get; set; } = [];
    public Anschrift Anschrift { get; set; } = new();
    public string? Website { get; set; }
    public string? Domain { get; set; }
    public string? Email { get; set; }
    public string? LogoPfad { get; set; }
    public LogoQuelle LogoQuelle { get; set; }
    public Guid? DefaultKategorieId { get; set; }
    public bool IstLieferant { get; set; }
    public bool IstKunde { get; set; }
    public Matching Matching { get; set; } = new();
    public DateTimeOffset ErstelltAm { get; set; }

    /// <summary><c>false</c> = „neu“, noch von keinem Benutzer geprüft.</summary>
    public bool Geprueft { get; set; }

    public Kategorie? DefaultKategorie { get; set; }

    /// <summary>VIES-Ergebnis für die aktuelle USt-IdNr.; nach einer Änderung der Nummer wieder „ungeprüft“.</summary>
    public UstIdStatus AktuellerUstIdStatus() =>
        UstIdNr is not null && UstIdGeprueftNummer == UstIdNr ? UstIdStatus : UstIdStatus.Ungeprueft;
}
