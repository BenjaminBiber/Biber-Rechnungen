namespace Biber.Invoice.Contracts.Dto.Export;

/// <summary>Export-Auftrag. <see cref="BeleglisteFormat"/>: „xlsx“ oder „csv“.</summary>
public sealed record ExportAnfrageDto(
    DateOnly Von,
    DateOnly Bis,
    string Bezeichnung,
    bool Belegliste,
    string BeleglisteFormat,
    bool SummenJeKategorie,
    bool Vorsteuer,
    bool ZipOriginale,
    Guid[] KategorieIds,
    Guid[] KorrespondentIds,
    Guid[] TagIds,
    bool DublettenAusschliessen,
    bool ZuPruefenAusschliessen);

public sealed record ExportVorschauDto(
    int AnzahlBelege,
    decimal SummeBrutto,
    decimal Vorsteuer,
    int DublettenAusgeschlossen,
    int AnzahlZuPruefen,
    long? ZipGroesse,
    ExportKategorieZeileDto[] Kategorien);

public sealed record ExportKategorieZeileDto(string Kategorie, string? EuerZeile, decimal Brutto);

/// <summary>Ein erzeugter Export; <see cref="Inhalt"/> beschreibt die enthaltenen Teile für die Liste.</summary>
public sealed record ExportLaufDto(
    Guid Id,
    DateTimeOffset ErstelltAm,
    string Bezeichnung,
    string Inhalt,
    int AnzahlBelege,
    DateOnly Von,
    DateOnly Bis,
    string DownloadUrl);
