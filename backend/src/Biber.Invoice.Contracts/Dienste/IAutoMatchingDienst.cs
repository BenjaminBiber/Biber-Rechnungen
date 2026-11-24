using Biber.Invoice.Contracts.Dto.Tags;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Trainingsstand des Auto-Matchings (ML.NET).</summary>
public interface IAutoMatchingDienst
{
    /// <summary>Je vorhandenem Modell (Tag, Kategorie, Korrespondent) Zeitpunkt, Anzahl Beispiele und Güte.</summary>
    Task<AutoMatchingStatusDto[]> StatusAsync(CancellationToken ct);
}
