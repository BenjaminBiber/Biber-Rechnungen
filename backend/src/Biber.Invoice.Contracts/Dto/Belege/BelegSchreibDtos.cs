using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Belege;

public enum MassenaktionArt
{
    TagsSetzen,
    TagsEntfernen,
    KategorieSetzen,
    Bestaetigen,
}

public sealed record BelegAendernDto(
    Guid? KorrespondentId,
    Belegart Belegart,
    string? Rechnungsnummer,
    string Titel,
    DateOnly? Rechnungsdatum,
    DateOnly? Leistungsdatum,
    DateOnly? LeistungVon,
    DateOnly? LeistungBis,
    DateOnly? Faelligkeitsdatum,
    Guid? KategorieId,
    string Waehrung,
    decimal? Netto,
    decimal? Steuer,
    decimal? Brutto,
    SteuerzeileAendernDto[] Steuerzeilen);

public sealed record SteuerzeileAendernDto(decimal Satz, decimal Netto, decimal Steuer);

/// <param name="Artikelnummer">Bestimmt den Artikel; Positionen mit gleicher Nummer teilen sich einen.</param>
/// <param name="Merkmale">Größe, Farbe o. Ä.; ohne Angabe keine.</param>
public sealed record PositionAendernDto(
    int Nr,
    string Text,
    decimal? Menge,
    string? Einheit,
    decimal? Einzelpreis,
    decimal Gesamt,
    decimal? Steuersatz,
    string? Artikelnummer = null,
    MerkmalDto[]? Merkmale = null);

public sealed record TagZuordnenDto(Guid TagId);

public sealed record BestaetigenDto(string? Begruendung);

/// <summary>Markiert den Beleg als Dublette von <see cref="DuplikatVonId"/>; <c>null</c> hebt die Markierung auf.</summary>
public sealed record DubletteSetzenDto(Guid? DuplikatVonId);

/// <summary>Verknüpft eine Gutschrift mit der Rechnung <see cref="BezugBelegId"/>; <c>null</c> löst die Verknüpfung.</summary>
public sealed record BezugSetzenDto(Guid? BezugBelegId);

/// <summary>
/// Setzt das Skonto einer Rechnung; <see cref="Prozent"/> <c>null</c> entfernt es. Den Betrag errechnet der Server aus dem
/// Bruttobetrag.
/// </summary>
public sealed record SkontoSetzenDto(decimal? Prozent, DateOnly? Bis);

public sealed record MassenaktionDto(Guid[] BelegIds, MassenaktionArt Aktion, Guid[]? TagIds, Guid? KategorieId);

public sealed record AufteilenDto(AufteilenTeilDto[] Teile);

/// <summary>Ein neuer Teilbeleg aus den genannten Seiten (ab 1).</summary>
public sealed record AufteilenTeilDto(int[] Seiten);

/// <summary>Ausschnitt einer Seite als Logo; Koordinaten relativ zur Seite (0–1).</summary>
public sealed record LogoAusschnittDto(int Seite, double X, double Y, double Breite, double Hoehe);
