using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>WORM-Ablage der Originale unter <c>Pfade:Ablage</c>: jede Datei wird genau einmal geschrieben und nie überschrieben.</summary>
public interface IAblageSchreiber
{
    /// <summary>
    /// Schreibt <paramref name="inhalt"/> schreibgeschützt an den Ablagepfad des Belegs und hängt die neue
    /// <see cref="BelegDatei"/> an <see cref="Beleg.Dateien"/> an. <paramref name="endung"/> klein, ohne Punkt.
    /// </summary>
    Task<BelegDatei> AblegenAsync(Beleg beleg, Stream inhalt, DateiRolle rolle, string endung, CancellationToken ct);

    Stream Oeffnen(BelegDatei datei);

    string AbsoluterPfad(BelegDatei datei);
}
