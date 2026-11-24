using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Liefert Seitenbilder eines Belegs als WebP und hält sie unter <c>Pfade:Daten/vorschau</c> vor.</summary>
public interface IVorschauDienst
{
    /// <summary>
    /// WebP der Seite <paramref name="seite"/> (ab 1) in der Breite <paramref name="breitePx"/>;
    /// <c>null</c>, wenn der Beleg keine Vorschau hat (reines XML, unbekanntes Format) oder es die Seite nicht gibt.
    /// </summary>
    /// <param name="quellPfad">Absoluter Pfad der Quelldatei, solange das Original noch nicht abgelegt ist.</param>
    Task<byte[]?> SeiteAsync(Beleg beleg, int seite, int breitePx, CancellationToken ct, string? quellPfad = null);

    /// <summary>Entfernt das zwischengespeicherte Bild, damit es beim nächsten Abruf neu entsteht.</summary>
    void Verwerfen(Beleg beleg, int seite, int breitePx);

    /// <summary>Pfad des zwischengespeicherten Bilds relativ zu <c>Pfade:Daten</c>.</summary>
    string RelativerPfad(Beleg beleg, int seite, int breitePx);
}
