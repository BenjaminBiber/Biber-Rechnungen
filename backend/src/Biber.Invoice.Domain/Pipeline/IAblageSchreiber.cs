using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>
/// WORM-Ablage der Originale unter <c>Pfade:Ablage</c>: jede Datei wird genau einmal geschrieben und nie überschrieben.
/// Solange ein Beleg „zu prüfen“ ist, darf sie an ihren neuen Ablagepfad umziehen; der Inhalt bleibt byte-gleich.
/// </summary>
public interface IAblageSchreiber
{
    /// <summary>
    /// Schreibt <paramref name="inhalt"/> schreibgeschützt an den Ablagepfad des Belegs und hängt die neue
    /// <see cref="BelegDatei"/> an <see cref="Beleg.Dateien"/> an. <paramref name="endung"/> klein, ohne Punkt.
    /// </summary>
    Task<BelegDatei> AblegenAsync(Beleg beleg, Stream inhalt, DateiRolle rolle, string endung, CancellationToken ct);

    /// <summary>
    /// Verschiebt Original und eingebettetes XML des Belegs an den Pfad, der sich aus seinen aktuellen Kopfdaten ergibt,
    /// und setzt <see cref="BelegDatei.Pfad"/> neu (noch nicht gespeichert). Inhalt, Hash und Schreibschutz bleiben.
    /// Leere alte Ordner werden entfernt. Liefert die Verschiebungen für <see cref="Zuruecknehmen"/>.
    /// </summary>
    Task<IReadOnlyList<AblageVerschiebung>> VerschiebenAsync(Beleg beleg, CancellationToken ct);

    /// <summary>Ordner unter der Ablage-Wurzel für die Originale gelöschter (falsch zugeordneter) Belege.</summary>
    const string GeloeschtOrdner = "_Geloescht";

    /// <summary>
    /// Verschiebt Original und eingebettetes XML eines gelöschten Belegs nach <c>_Geloescht/{bisheriger Pfad}</c>
    /// (noch nicht gespeichert). Zurück an den regulären Pfad bringt sie <see cref="VerschiebenAsync"/>.
    /// </summary>
    Task<IReadOnlyList<AblageVerschiebung>> InGeloeschtVerschiebenAsync(Beleg beleg, CancellationToken ct);

    /// <summary>Macht <see cref="VerschiebenAsync"/> rückgängig, z. B. wenn das Speichern danach scheitert.</summary>
    void Zuruecknehmen(IReadOnlyList<AblageVerschiebung> verschiebungen);

    Stream Oeffnen(BelegDatei datei);

    string AbsoluterPfad(BelegDatei datei);
}

/// <summary>Eine umgezogene Ablage-Datei mit ihren relativen Pfaden vorher und nachher.</summary>
public sealed record AblageVerschiebung(BelegDatei Datei, string Von, string Nach);
