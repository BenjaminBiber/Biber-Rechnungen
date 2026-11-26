namespace Biber.Invoice.Contracts.Dto.Tags;

public sealed record TagDto(
    Guid Id,
    string Name,
    string Farbe,
    bool IstInbox,
    bool IstZahlungsTag,
    bool IstSystem,
    MatchingDto Matching,
    int AnzahlBelege);

public sealed record TagAendernDto(string Name, string Farbe, bool IstInbox, bool IstZahlungsTag, MatchingDto Matching);

/// <summary>Beim Löschen einer Kategorie betroffene Einträge.</summary>
public sealed record KategorieGeloeschtDto(int Belege, int Korrespondenten);

/// <summary>Kategorie mit Kennzahlen des gewählten Jahres.</summary>
public sealed record KategorieDto(
    Guid Id,
    string Name,
    string Farbe,
    string? EuerZeile,
    MatchingDto Matching,
    int AnzahlBelege,
    decimal SummeBrutto,
    Guid? ElternId);

/// <summary><see cref="ElternId"/>: Oberkategorie; leer = Hauptkategorie. Nur eine Ebene.</summary>
public sealed record KategorieAendernDto(string Name, string Farbe, string? EuerZeile, MatchingDto Matching, Guid? ElternId = null);

/// <summary>Testet eine Regel gegen den Bestand. <see cref="Ziel"/>: „Tag“, „Kategorie“ oder „Korrespondent“.</summary>
public sealed record MatchingTestDto(string Ziel, Guid? ZielId, MatchingDto Matching);

public sealed record MatchingTestErgebnisDto(
    int Treffer,
    int Gesamt,
    int OhneZuweisung,
    string? Auszug,
    int? TrefferStart,
    int? TrefferLaenge,
    string? LetzterTreffer);

/// <summary>Wendet die gespeicherte Regel des Ziels auf alle Belege an.</summary>
public sealed record MatchingAnwendenDto(string Ziel, Guid ZielId);

public sealed record AutoMatchingStatusDto(string Ziel, DateTimeOffset? TrainiertAm, int AnzahlBeispiele, decimal? Guete);
