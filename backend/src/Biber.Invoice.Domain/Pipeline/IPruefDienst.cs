using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Führt die Plausibilitätsprüfungen an einem Beleg aus und ersetzt dessen Befunde.</summary>
public interface IPruefDienst
{
    /// <summary>
    /// Prüft <paramref name="beleg"/> und ersetzt seine <see cref="PruefBefund"/>e. Mit <paramref name="kontext"/>
    /// (Lauf in der Pipeline) wird bei einem fehlgeschlagenen Befund der Tag „zu prüfen“ gesetzt. Ohne Kontext
    /// (erneutes Prüfen nach einer Korrektur) werden nur die Befunde aktualisiert; der Tag und der KoSIT-Befund bleiben.
    /// Gespeichert wird nicht, das übernimmt der Aufrufer.
    /// </summary>
    /// <returns>Die neuen Befunde.</returns>
    Task<IReadOnlyList<PruefBefund>> PruefenAsync(Beleg beleg, VerarbeitungsKontext? kontext, CancellationToken ct);
}
