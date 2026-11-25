using Biber.Invoice.Contracts.Dto.Artikel;
using Biber.Invoice.Contracts.Dto.Belege;

namespace Biber.Invoice.Contracts.Dto.Korrespondenten;

public sealed record KorrespondentListeneintragDto(
    Guid Id,
    string Name,
    LogoDto Logo,
    string? Hinweis,
    string? UstIdNr,
    string? Steuernummer,
    VerweisDto? DefaultKategorie,
    MatchingDto Matching,
    int AnzahlBelege,
    decimal KostenJahr,
    DateOnly? LetzterBeleg,
    bool IstNeu);

/// <summary>Detail eines Korrespondenten: Stammdaten, Kennzahlen des gewählten Jahres, letzte Belege und Artikel.</summary>
public sealed record KorrespondentDetailDto(
    Guid Id,
    string Name,
    string[] Aliase,
    string? Anschrift,
    string? UstIdNr,
    string? Steuernummer,
    BankverbindungDto[] Bankverbindungen,
    string? Website,
    string? Email,
    bool IstLieferant,
    bool IstKunde,
    LogoDto Logo,
    VerweisDto? DefaultKategorie,
    MatchingDto Matching,
    DateTimeOffset? AutoTrainiertAm,
    KorrespondentKennzahlenDto Kennzahlen,
    decimal[] KostenverlaufMonate,
    BelegListeneintragDto[] LetzteBelege,
    ArtikelListeneintragDto[] Artikel,
    int AnzahlArtikel);

/// <summary>IBAN ohne Leerzeichen in Großbuchstaben; der BIC ist optional.</summary>
public sealed record BankverbindungDto(string Iban, string? Bic);

public sealed record KorrespondentKennzahlenDto(
    decimal KostenJahr,
    decimal? AenderungVorjahr,
    int AnzahlBelege,
    DateOnly? Seit,
    decimal DurchschnittJeBeleg,
    int OffenAnzahl,
    decimal OffenSumme,
    DateOnly? NaechsteFaelligkeit);

public sealed record KorrespondentAendernDto(
    string Name,
    string[] Aliase,
    string? Anschrift,
    string? UstIdNr,
    string? Steuernummer,
    BankverbindungDto[] Bankverbindungen,
    string? Website,
    string? Email,
    bool IstLieferant,
    bool IstKunde,
    Guid? DefaultKategorieId,
    MatchingDto Matching);

/// <summary>Führt die Korrespondenten <see cref="QuellIds"/> in <see cref="BehaltenId"/> zusammen.</summary>
public sealed record KorrespondentenZusammenfuehrenDto(Guid BehaltenId, Guid[] QuellIds);
