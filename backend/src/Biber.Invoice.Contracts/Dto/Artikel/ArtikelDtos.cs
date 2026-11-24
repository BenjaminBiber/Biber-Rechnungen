namespace Biber.Invoice.Contracts.Dto.Artikel;

public enum PreisaenderungFilter
{
    Gestiegen,
    Gefallen,
}

/// <summary>Filter der Artikelliste; kommt als Query-String.</summary>
public sealed record ArtikelFilterDto
{
    public string? Suche { get; init; }

    public Guid? KorrespondentId { get; init; }

    public bool OhneStammartikel { get; init; }

    public PreisaenderungFilter? Preisaenderung { get; init; }

    public int Seite { get; init; } = 1;

    public int Seitengroesse { get; init; } = 50;
}

public sealed record ArtikelListeneintragDto(
    Guid Id,
    string Name,
    VerweisDto Korrespondent,
    VerweisDto? Stammartikel,
    int Kaeufe,
    decimal? LetzterEinzelpreis,
    string? Einheit,
    decimal? Grundpreis,
    string? GrundpreisEinheit,
    decimal? AenderungSeitErstkauf,
    DateOnly? ZuletztGekauft);

/// <summary>Stammartikel in der Liste; <see cref="Verlauf"/> sind die Grundpreise für die Sparkline.</summary>
public sealed record StammartikelListeneintragDto(
    Guid Id,
    string Name,
    string Basiseinheit,
    decimal GrundpreisMenge,
    int AnzahlArtikel,
    string[] Korrespondenten,
    decimal[] Verlauf,
    decimal? GuenstigsterPreis,
    string? GuenstigsterBei,
    decimal? Aenderung);

/// <summary>Preisdetail eines Artikels oder Stammartikels für das gewählte Jahr.</summary>
public sealed record PreisDetailDto(
    Guid Id,
    string Name,
    bool IstStammartikel,
    string? Basiseinheit,
    decimal? GrundpreisMenge,
    PreisKennzahlenDto Kennzahlen,
    PreisSerieDto[] Serien,
    KaufDto[] Kaeufe,
    ZugeordneterArtikelDto[] Artikel);

public sealed record PreisKennzahlenDto(
    decimal? GuenstigsterGrundpreis,
    string? GuenstigsterBei,
    decimal? AenderungSeitJahresbeginn,
    decimal? PreisVon,
    decimal? PreisBis,
    decimal MengeJahr,
    decimal AusgabenJahrNetto,
    int AnzahlKaeufe);

/// <summary>Preisverlauf bei einem Korrespondenten.</summary>
public sealed record PreisSerieDto(VerweisDto Korrespondent, string? Einheit, decimal? UmrechnungFaktor, PreisPunktDto[] Punkte);

public sealed record PreisPunktDto(DateOnly Datum, decimal? Einzelpreis, decimal? Grundpreis);

public sealed record KaufDto(
    DateOnly Datum,
    VerweisDto Korrespondent,
    Guid BelegId,
    string? Rechnungsnummer,
    decimal? Menge,
    string? Einheit,
    decimal? Einzelpreis,
    decimal? Grundpreis);

public sealed record ZugeordneterArtikelDto(Guid Id, string Name, VerweisDto Korrespondent, string? Einheit, decimal? UmrechnungFaktor);

public sealed record StammartikelAendernDto(string Name, string Basiseinheit, decimal GrundpreisMenge, ArtikelZuordnungDto[] Artikel);

public sealed record ArtikelZuordnungDto(Guid ArtikelId, decimal? UmrechnungFaktor);
