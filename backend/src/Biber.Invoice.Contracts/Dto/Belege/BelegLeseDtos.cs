using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Belege;

public sealed record BelegListeDto(
    IReadOnlyList<BelegListeneintragDto> Eintraege,
    int Gesamt,
    int Seite,
    int Seitengroesse,
    decimal SummeBrutto);

/// <summary>
/// Ein Beleg in Liste, Karte, Suche und Dubletten. <see cref="Format"/>: „XRechnung“, „ZUGFeRD“, „PDF“ oder „Scan“;
/// <see cref="Skonto"/> ist <c>null</c>, wenn kein Skonto bekannt ist.
/// </summary>
public sealed record BelegListeneintragDto(
    Guid Id,
    string? Rechnungsnummer,
    VerweisDto? Korrespondent,
    LogoDto Logo,
    string Titel,
    VerweisDto? Kategorie,
    DateOnly? Rechnungsdatum,
    DateOnly? Faelligkeitsdatum,
    bool Ueberfaellig,
    decimal? Netto,
    decimal? Brutto,
    string Waehrung,
    int Seitenanzahl,
    ImportQuelle Quelle,
    string Format,
    TagKurzDto[] Tags,
    string Auszug,
    string? VorschauUrl,
    bool IstDublette,
    decimal? Konfidenz,
    DateOnly? Leistungsdatum,
    SkontoDto? Skonto);

public sealed record BelegZaehlerDto(int Alle, int Inbox, int ZuPruefen, int Offen, int Ueberfaellig);

/// <summary>Beleg-Detail: Kopf wie <see cref="BelegListeneintragDto"/> und alle Tabs der Detailansicht.</summary>
public sealed record BelegDetailDto(
    Guid Id,
    string? Rechnungsnummer,
    VerweisDto? Korrespondent,
    LogoDto Logo,
    string Titel,
    VerweisDto? Kategorie,
    DateOnly? Rechnungsdatum,
    DateOnly? Faelligkeitsdatum,
    bool Ueberfaellig,
    decimal? Netto,
    decimal? Brutto,
    string Waehrung,
    int Seitenanzahl,
    ImportQuelle Quelle,
    string Format,
    TagKurzDto[] Tags,
    string Auszug,
    string? VorschauUrl,
    bool IstDublette,
    decimal? Konfidenz,
    DateOnly? Leistungsdatum,
    Belegart Belegart,
    BelegStatus Status,
    KorrespondentErkennung KorrespondentErkennung,
    BankverbindungDto[] Bankverbindungen,
    bool KategorieManuell,
    VerweisDto? DefaultKategorie,
    DateOnly? LeistungVon,
    DateOnly? LeistungBis,
    decimal? Steuer,
    SteuerzeileDto[] Steuerzeilen,
    PositionDto[] Positionen,
    PruefBefundDto[] Befunde,
    BelegDateiDto[] Dateien,
    ExtraktionDto Extraktion,
    DubletteDto Dubletten,
    DateOnly LoeschsperreBis,
    bool IstERechnung,
    Guid? AufgeteiltAusBelegId,
    Guid[] Teilbelege,
    GutschriftBezugDto Bezug,
    SkontoDto? Skonto);

/// <summary>Kurzverweis auf eine Rechnung oder Gutschrift, auf die sich ein Beleg bezieht.</summary>
public sealed record BezugVerweisDto(Guid Id, string? Rechnungsnummer, DateOnly? Rechnungsdatum, decimal? Brutto, string Waehrung);

/// <summary>
/// Verknüpfung zwischen Gutschrift und Rechnung. <see cref="Rechnung"/>: bei einer Gutschrift die Rechnung, auf die sie
/// sich bezieht. <see cref="Gutschriften"/>: bei einer Rechnung die verknüpften Gutschriften; <see cref="Restbetrag"/> ist
/// dann Brutto abzüglich dieser Gutschriften (sonst <c>null</c>). <see cref="Kandidaten"/>: bei einer Gutschrift die
/// Rechnungen desselben Korrespondenten zur Auswahl, neueste zuerst.
/// </summary>
public sealed record GutschriftBezugDto(
    BezugVerweisDto? Rechnung,
    BezugVerweisDto[] Gutschriften,
    decimal? Restbetrag,
    BezugVerweisDto[] Kandidaten);

/// <summary>Skonto der Rechnung; <see cref="Abgelaufen"/>: die Frist liegt vor heute.</summary>
public sealed record SkontoDto(decimal Prozent, DateOnly? Bis, decimal? Betrag, bool Abgelaufen);

/// <summary>Steuerzeile; <see cref="Stimmt"/> sagt, ob Netto + Steuer zum Satz passen.</summary>
public sealed record SteuerzeileDto(decimal Satz, decimal Netto, decimal Steuer, bool Stimmt);

/// <param name="Artikelnummer">Artikelnummer des Lieferanten, wie auf dem Beleg.</param>
/// <param name="Variante">Größe, Farbe o. Ä.; gehört zur Position, nicht zum Artikel.</param>
public sealed record PositionDto(
    int Nr,
    string Text,
    string? Artikelnummer,
    string? Variante,
    decimal? Menge,
    string? Einheit,
    decimal? Einzelpreis,
    decimal Gesamt,
    decimal? Steuersatz,
    VerweisDto? Artikel,
    Guid? StammartikelId,
    decimal? Grundpreis,
    string? GrundpreisEinheit);

public sealed record PruefBefundDto(PruefungArt Pruefung, string Name, PruefErgebnis Ergebnis, string Detail);

public sealed record BelegDateiDto(
    Guid Id,
    DateiRolle Rolle,
    string MimeType,
    string Pfad,
    string Sha256,
    long Groesse,
    DateTimeOffset AbgelegtAm,
    DateTimeOffset? LetztePruefungAm,
    bool? PruefsummeOk,
    string Url);

public sealed record ExtraktionDto(ExtraktionsQuelle Quelle, decimal? Konfidenz, DateTimeOffset? Zeitpunkt, int Seiten);

public sealed record DubletteDto(
    VerweisDto? DuplikatVon,
    BelegListeneintragDto[] Duplikate,
    bool HashTreffer,
    bool FachlicherTreffer);

/// <summary>Vorheriger und nächster Beleg in der gefilterten Liste; <see cref="Position"/> beginnt bei 1.</summary>
public sealed record BelegNachbarnDto(Guid? VorherId, Guid? NaechsteId, int Position, int Gesamt);

/// <summary>Eintrag der Historie. <see cref="Art"/> steuert Icon und Farbe in der UI.</summary>
public sealed record AuditEintragDto(
    Guid Id,
    DateTimeOffset Zeitpunkt,
    string Wer,
    bool IstSystem,
    string Aktion,
    string Text,
    string? Feld,
    string? AlterWert,
    string? NeuerWert,
    string Art);

/// <summary>Gespeicherte Ansicht der Belegliste; <see cref="Darstellung"/>: „Tabelle“, „KleineKarten“ oder „GrosseKarten“.</summary>
public sealed record GespeicherteAnsichtDto(Guid Id, string Name, BelegFilterDto Filter, string[] Spalten, string Darstellung);

public sealed record GespeicherteAnsichtAnlegenDto(string Name, BelegFilterDto Filter, string[] Spalten, string Darstellung);
