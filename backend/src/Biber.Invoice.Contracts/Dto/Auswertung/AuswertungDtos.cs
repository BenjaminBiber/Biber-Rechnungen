using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Auswertung;

public enum KostenGruppierung
{
    Monat,
    Kategorie,
    Korrespondent,
}

/// <summary>Dashboard; <c>zeitraum</c> ist „Monat“, „Quartal“ oder „Jahr“, Zeitbasis immer das Rechnungsdatum.</summary>
public sealed record DashboardDto(
    DashboardKennzahlDto Kosten,
    DashboardKennzahlDto KostenJahr,
    DashboardKennzahlDto Offen,
    DashboardKennzahlDto Ueberfaellig,
    DashboardKennzahlDto Inbox,
    DashboardKennzahlDto ZuPruefen,
    MonatsKostenDto[] KostenProMonat,
    TopKorrespondentDto[] TopKorrespondenten,
    ZuPruefenEintragDto[] ZuPruefenListe,
    OffenEintragDto[] OffeneBelege,
    PreisaenderungDto[] Preisaenderungen,
    VorsteuerDto? Vorsteuer,
    DiVerbrauchDto DiVerbrauch);

/// <summary>
/// Kachel des Dashboards. <see cref="AenderungProzent"/> vergleicht mit <see cref="Vergleichszeitraum"/>;
/// <see cref="AeltesteTage"/> ist bei „Überfällig“ das Alter des ältesten Belegs in Tagen.
/// </summary>
public sealed record DashboardKennzahlDto(
    int Anzahl,
    decimal? Betrag,
    decimal? AenderungProzent,
    string? Vergleichszeitraum,
    int? AeltesteTage);

/// <summary>Kosten eines Monats; <see cref="Monat"/> im Format <c>yyyy-MM</c>.</summary>
public sealed record MonatsKostenDto(string Monat, decimal Summe, decimal SummeVorjahr, KategorieAnteilDto[] NachKategorie);

/// <summary>Anteil einer Kategorie; <see cref="Kategorie"/> ist <c>null</c> für „ohne Kategorie“.</summary>
public sealed record KategorieAnteilDto(VerweisDto? Kategorie, string Farbe, decimal Summe);

public sealed record TopKorrespondentDto(VerweisDto Korrespondent, LogoDto Logo, decimal Summe, int AnzahlBelege);

public sealed record ZuPruefenEintragDto(Guid BelegId, string? Korrespondent, string? Rechnungsnummer, decimal? Brutto, string Grund);

public sealed record OffenEintragDto(
    Guid BelegId,
    string? Korrespondent,
    string? Rechnungsnummer,
    decimal? Brutto,
    DateOnly? Faelligkeitsdatum,
    bool Ueberfaellig);

public sealed record PreisaenderungDto(
    Guid Id,
    string Name,
    bool IstStammartikel,
    string? Korrespondent,
    decimal? PreisAlt,
    decimal? PreisNeu,
    string? Einheit,
    decimal Aenderung);

/// <summary>Vorsteuer je UStVA-Zeitraum; fehlt bei Kleinunternehmern.</summary>
public sealed record VorsteuerDto(UstvaZeitraum Zeitraum, VorsteuerZeileDto[] Zeilen, decimal Summe);

/// <summary>Vorsteuer eines Zeitraums, z. B. „Q3 2026“.</summary>
public sealed record VorsteuerZeileDto(string Bezeichnung, decimal Betrag);

public sealed record SystemStatusEintragDto(
    string Schluessel,
    Schweregrad Schweregrad,
    string Titel,
    string Text,
    DateTimeOffset? Zeitpunkt);

/// <summary>Kostenauswertung für einen Zeitraum (MCP-Tool <c>kosten_auswerten</c>).</summary>
public sealed record KostenAuswertungDto(KostenZeileDto[] Zeilen, decimal SummeBrutto);

public sealed record KostenZeileDto(
    string Schluessel,
    string Bezeichnung,
    decimal Netto,
    decimal Steuer,
    decimal Brutto,
    int AnzahlBelege);
