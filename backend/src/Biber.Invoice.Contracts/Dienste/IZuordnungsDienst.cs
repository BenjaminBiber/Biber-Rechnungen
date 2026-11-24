using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Weist Belegen Kategorie und Tags über Default-Kategorie und Matching-Regeln zu (Pipeline-Schritt 7).</summary>
public interface IZuordnungsDienst
{
    /// <summary>Setzt Kategorie (falls nicht manuell) und passende Tags; entfernt nie Tags.</summary>
    Task ZuordnenAsync(Beleg beleg, CancellationToken ct);

    /// <summary>Wertet eine noch nicht gespeicherte Regel gegen den Bestand aus.</summary>
    /// <exception cref="ArgumentException">Unbekanntes Ziel.</exception>
    Task<MatchingTestErgebnisDto> TestenAsync(MatchingTestDto anfrage, CancellationToken ct);

    /// <summary>Wendet die gespeicherte Regel von Tag oder Kategorie auf alle bestehenden Belege an.</summary>
    /// <returns>Anzahl geänderter Belege.</returns>
    /// <exception cref="ArgumentException">Ziel ist „Korrespondent“ oder unbekannt.</exception>
    /// <exception cref="KeyNotFoundException">Tag bzw. Kategorie existiert nicht.</exception>
    Task<int> AnwendenAsync(string ziel, Guid zielId, CancellationToken ct);
}
