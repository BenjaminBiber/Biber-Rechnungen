using SkiaSharp;

namespace Biber.Invoice.Infrastructure.Module.Logos;

/// <summary>Bildverarbeitung für Logos: Rasterbild lesen, transparenten Rand entfernen, auf höchstens 256×256 verkleinern, als PNG kodieren.</summary>
public static class LogoBild
{
    public const int MaxKante = 256;

    /// <summary>Liefert das normalisierte PNG oder <c>null</c>, wenn der Inhalt kein lesbares Rasterbild ist.</summary>
    public static byte[]? AlsLogoPng(byte[] inhalt)
    {
        using var bild = Lesen(inhalt);
        return bild is null ? null : AlsLogoPng(bild);
    }

    public static byte[] AlsLogoPng(SKBitmap bild)
    {
        var rahmen = SichtbarerBereich(bild);
        using var zugeschnitten = new SKBitmap();
        var quelle = rahmen == new SKRectI(0, 0, bild.Width, bild.Height) || !bild.ExtractSubset(zugeschnitten, rahmen)
            ? bild
            : zugeschnitten;

        var faktor = Math.Min(1.0, (double)MaxKante / Math.Max(quelle.Width, quelle.Height));
        if (faktor >= 1.0)
        {
            return Png(quelle);
        }

        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Round(quelle.Width * faktor)),
            Math.Max(1, (int)Math.Round(quelle.Height * faktor)),
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul);
        using var skaliert = quelle.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        return Png(skaliert);
    }

    /// <summary>Dekodiert ein Rasterbild (PNG, JPG, WebP, ICO, …); <c>null</c>, wenn SkiaSharp den Inhalt nicht lesen kann.</summary>
    public static SKBitmap? Lesen(byte[] inhalt)
    {
        if (inhalt.Length == 0)
        {
            return null;
        }

        using var codec = SKCodec.Create(new MemoryStream(inhalt, writable: false));
        if (codec is null)
        {
            return null;
        }

        var info = codec.Info.WithColorType(SKColorType.Rgba8888).WithAlphaType(SKAlphaType.Unpremul);
        var bild = new SKBitmap(info);
        var ergebnis = codec.GetPixels(info, bild.GetPixels());
        if (ergebnis is SKCodecResult.Success or SKCodecResult.IncompleteInput)
        {
            return bild;
        }

        bild.Dispose();
        return null;
    }

    public static SKEncodedImageFormat? Format(byte[] inhalt)
    {
        using var codec = inhalt.Length == 0 ? null : SKCodec.Create(new MemoryStream(inhalt, writable: false));
        return codec?.EncodedFormat;
    }

    private static SKRectI SichtbarerBereich(SKBitmap bild)
    {
        int links = bild.Width, oben = bild.Height, rechts = -1, unten = -1;
        for (var y = 0; y < bild.Height; y++)
        {
            for (var x = 0; x < bild.Width; x++)
            {
                if (bild.GetPixel(x, y).Alpha == 0)
                {
                    continue;
                }

                links = Math.Min(links, x);
                rechts = Math.Max(rechts, x);
                oben = Math.Min(oben, y);
                unten = Math.Max(unten, y);
            }
        }

        // Ganz transparent: das Bild bleibt, wie es ist.
        return rechts < 0
            ? new SKRectI(0, 0, bild.Width, bild.Height)
            : new SKRectI(links, oben, rechts + 1, unten + 1);
    }

    private static byte[] Png(SKBitmap bild)
    {
        using var daten = bild.Encode(SKEncodedImageFormat.Png, 100);
        return daten.ToArray();
    }
}
