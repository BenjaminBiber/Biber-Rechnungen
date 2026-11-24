using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Belege;

public enum BelegSuchfeld
{
    TitelInhalt,
    Rechnungsnummer,
}

public enum BelegAnsicht
{
    Alle,
    Inbox,
    ZuPruefen,
    Offen,
    Ueberfaellig,
}

public enum BetragArt
{
    Brutto,
    Netto,
}

public enum BelegSortierung
{
    Rechnungsdatum,
    Faelligkeit,
    Brutto,
    Korrespondent,
    ErstelltAm,
}

/// <summary>Filter der Belegliste; kommt als Query-String und steckt in gespeicherten Ansichten.</summary>
public sealed record BelegFilterDto
{
    public string? Suche { get; init; }

    public BelegSuchfeld Suchfeld { get; init; } = BelegSuchfeld.TitelInhalt;

    public BelegAnsicht Ansicht { get; init; } = BelegAnsicht.Alle;

    public Guid[] TagIds { get; init; } = [];

    public Guid[] KorrespondentIds { get; init; } = [];

    public Guid[] KategorieIds { get; init; } = [];

    /// <summary>Rechnungsdatum ab (einschließlich).</summary>
    public DateOnly? Von { get; init; }

    /// <summary>Rechnungsdatum bis (einschließlich).</summary>
    public DateOnly? Bis { get; init; }

    public decimal? BetragVon { get; init; }

    public decimal? BetragBis { get; init; }

    public BetragArt BetragArt { get; init; } = BetragArt.Brutto;

    public Zahlstatus? Zahlstatus { get; init; }

    public ImportQuelle[] Quellen { get; init; } = [];

    public bool DublettenAusschliessen { get; init; }

    public BelegSortierung Sortierung { get; init; } = BelegSortierung.Rechnungsdatum;

    public bool Absteigend { get; init; } = true;

    public int Seite { get; init; } = 1;

    public int Seitengroesse { get; init; } = 50;
}
