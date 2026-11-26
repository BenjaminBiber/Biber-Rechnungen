using Biber.Invoice.Contracts.Dto.Artikel;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Sonstiges;

public sealed record BenachrichtigungDto(
    Guid Id,
    BenachrichtigungArt Art,
    Schweregrad Schweregrad,
    string Titel,
    string Text,
    string? Link,
    DateTimeOffset ErstelltAm,
    bool Gelesen);

public sealed record BenachrichtigungZaehlerDto(int Ungelesen);

/// <summary>Belege eines Jahres, deren Aufbewahrungsfrist abgelaufen ist.</summary>
public sealed record LoeschbarDto(int Jahr, int Anzahl, DateOnly FristEnde);

/// <summary>Löschen der DB-Daten eines Jahres; <see cref="Bestaetigung"/> ist das eingetippte Wort LÖSCHEN.</summary>
public sealed record LoeschenDto(int Jahr, string Bestaetigung);

/// <summary>Ergebnis der Löschung; <see cref="Pfade"/> löscht der Admin auf dem Host vom WORM-Share.</summary>
public sealed record LoeschErgebnisDto(int Geloescht, string[] Pfade);

public sealed record IntegritaetsLaufDto(
    Guid Id,
    DateTimeOffset GestartetAm,
    int? DauerSekunden,
    int AnzahlDateien,
    int AnzahlAbweichungen,
    string[] Abweichungen);

/// <summary>
/// „Alle Belege erneut auslesen“: wie viele Belege ein neuer Lauf umfassen würde und der jüngste Lauf
/// (<c>null</c>, solange noch keiner gestartet wurde).
/// </summary>
public sealed record NeuAuslesenStatusDto(int AnzahlBelege, NeuAuslesenLaufDto? Lauf);

/// <summary>
/// Stand eines Laufs. <see cref="Verarbeitet"/> zählt Fehler und übersprungene Belege mit; <see cref="RestSekunden"/>
/// schätzt die verbleibende Dauer aus dem bisherigen Tempo (nur während des Laufs und nach dem ersten Beleg).
/// </summary>
public sealed record NeuAuslesenLaufDto(
    Guid Id,
    DateTimeOffset GestartetAm,
    DateTimeOffset? BeendetAm,
    bool Laeuft,
    bool Abgebrochen,
    string? Abbruchgrund,
    int Gesamt,
    int Verarbeitet,
    int AnzahlFehler,
    int AnzahlUebersprungen,
    int? RestSekunden,
    int DauerSekunden,
    string? LetzterFehler,
    Guid[] FehlerBelege);

public sealed record BackupStatusDto(DateTimeOffset? LetztesBackup, long? Groesse, int AnzahlDumps);

public sealed record SuchergebnisDto(
    BelegListeneintragDto[] Belege,
    KorrespondentListeneintragDto[] Korrespondenten,
    ArtikelListeneintragDto[] Artikel,
    StammartikelListeneintragDto[] Stammartikel);

/// <summary>Persönliches API-Token; <see cref="Endung"/> sind die letzten Zeichen zum Wiedererkennen. Scopes: „lesen“, „upload“.</summary>
public sealed record ApiTokenDto(
    Guid Id,
    string Name,
    string Endung,
    string[] Scopes,
    DateTimeOffset ErstelltAm,
    DateTimeOffset? ZuletztGenutztAm,
    DateTimeOffset? WiderrufenAm);

public sealed record ApiTokenAnlegenDto(string Name, string[] Scopes);

/// <summary>Neu erzeugtes Token; <see cref="Klartext"/> wird nur dieses eine Mal geliefert.</summary>
public sealed record ApiTokenErstelltDto(ApiTokenDto Token, string Klartext);

public sealed record VerfahrensdokuDto(string Version, DateOnly Stand, int OffeneStellen, string SoftwareVersion);
