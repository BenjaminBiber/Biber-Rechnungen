using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto;

/// <summary>Eine Seite einer blätterbaren Liste. <see cref="Seite"/> beginnt bei 1.</summary>
public sealed record SeiteDto<T>(IReadOnlyList<T> Eintraege, int Gesamt, int Seite, int Seitengroesse);

/// <summary>Zuordnungsregel an Tags, Kategorien und Korrespondenten.</summary>
public sealed record MatchingDto(MatchingAlgorithmus Algorithmus, string? Muster, bool GrossKlein);

public sealed record TagKurzDto(Guid Id, string Name, string Farbe, bool IstInbox, bool IstZahlungsTag, bool IstSystem);

/// <summary>Verweis auf eine andere Entität mit ihrem Anzeigenamen.</summary>
public sealed record VerweisDto(Guid Id, string Name);

/// <summary>Merkmal einer Position wie Größe oder Farbe; <see cref="Name"/> fehlt, wenn der Beleg nur den Wert nennt.</summary>
public sealed record MerkmalDto(string? Name, string Wert);

/// <summary>Logo eines Korrespondenten; ohne <see cref="Url"/> zeigt die UI die Initialen.</summary>
public sealed record LogoDto(string? Url, string Initialen, LogoQuelle Quelle);

/// <summary>Ergebnis einer ausgelösten Aktion, z. B. eines Verbindungstests.</summary>
public sealed record AktionErgebnisDto(bool Erfolg, string Meldung);

/// <summary>Anzahl der durch eine Aktion geänderten Datensätze.</summary>
public sealed record AnzahlGeaendertDto(int Geaendert);
