using Biber.Invoice.Domain.Pipeline;
using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;

namespace Biber.Invoice.Infrastructure.Module.Vorschau;

/// <summary>Rendert PDF-Seiten mit PDFium (Docnet.Core). PDFium ist nicht threadsicher; jeder Zugriff läuft unter einem Lock.</summary>
public sealed class PdfRenderer : IPdfRenderer
{
    private static readonly Lock Sperre = new();

    public int Seitenanzahl(Stream pdf)
    {
        var bytes = Lesen(pdf);
        lock (Sperre)
        {
            using var dokument = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1d));
            return dokument.GetPageCount();
        }
    }

    public byte[] SeiteAlsPng(Stream pdf, int seite, int breitePx)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(breitePx, 1);
        var bytes = Lesen(pdf);
        byte[] pixel;
        int breite;
        int hoehe;
        lock (Sperre)
        {
            double seitenBreite;
            using (var dokument = DocLib.Instance.GetDocReader(bytes, new PageDimensions(1d)))
            {
                var anzahl = dokument.GetPageCount();
                if (seite < 1 || seite > anzahl)
                {
                    throw new ArgumentOutOfRangeException(nameof(seite), seite, $"Das PDF hat {anzahl} Seite(n).");
                }

                using var seitenLeser = dokument.GetPageReader(seite - 1);
                seitenBreite = seitenLeser.GetPageWidth();
            }

            using var skaliert = DocLib.Instance.GetDocReader(bytes, new PageDimensions(breitePx / seitenBreite));
            using var leser = skaliert.GetPageReader(seite - 1);
            breite = leser.GetPageWidth();
            hoehe = leser.GetPageHeight();
            pixel = leser.GetImage();
        }

        return AlsPng(pixel, breite, hoehe, breitePx);
    }

    /// <summary>PDFium liefert BGRA mit transparentem Hintergrund; das Bild wird auf Weiß gelegt und auf die Zielbreite gebracht.</summary>
    private static byte[] AlsPng(byte[] bgra, int breite, int hoehe, int zielBreite)
    {
        var info = new SKImageInfo(breite, hoehe, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var quelle = new SKBitmap();
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(bgra, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            quelle.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);
            var zielHoehe = Math.Max(1, (int)Math.Round(hoehe * (double)zielBreite / breite));
            using var flaeche = SKSurface.Create(new SKImageInfo(zielBreite, zielHoehe, SKColorType.Rgba8888, SKAlphaType.Premul));
            flaeche.Canvas.Clear(SKColors.White);
            using var bild = SKImage.FromBitmap(quelle);
            flaeche.Canvas.DrawImage(bild, new SKRect(0, 0, zielBreite, zielHoehe), new SKSamplingOptions(SKFilterMode.Linear));
            using var ergebnis = flaeche.Snapshot();
            using var daten = ergebnis.Encode(SKEncodedImageFormat.Png, 100);
            return daten.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    private static byte[] Lesen(Stream pdf)
    {
        using var kopie = new MemoryStream();
        pdf.CopyTo(kopie);
        return kopie.ToArray();
    }
}
