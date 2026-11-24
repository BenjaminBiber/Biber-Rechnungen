namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Rendert Seiten eines PDFs als Bild.</summary>
public interface IPdfRenderer
{
    int Seitenanzahl(Stream pdf);

    /// <param name="seite">Seite ab 1.</param>
    /// <param name="breitePx">Breite des Bilds in Pixeln; die Höhe folgt dem Seitenverhältnis.</param>
    /// <exception cref="ArgumentOutOfRangeException">Die Seite gibt es nicht oder die Breite ist nicht positiv.</exception>
    byte[] SeiteAlsPng(Stream pdf, int seite, int breitePx);
}
