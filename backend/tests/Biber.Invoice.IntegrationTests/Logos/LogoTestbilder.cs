using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace Biber.Invoice.IntegrationTests.Logos;

/// <summary>Erzeugt Testbilder, ICO-Dateien und ein PDF mit farbigem Rechteck.</summary>
internal static class LogoTestbilder
{
    /// <summary>Einfarbiges Bild; mit <paramref name="rand"/> Pixeln transparentem Rand rundherum.</summary>
    public static byte[] Png(int breite, int hoehe, SKColor farbe, int rand = 0, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bild = new SKBitmap(new SKImageInfo(breite, hoehe, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bild.Erase(SKColors.Transparent);
        using (var leinwand = new SKCanvas(bild))
        {
            using var pinsel = new SKPaint { Color = farbe };
            leinwand.DrawRect(new SKRect(rand, rand, breite - rand, hoehe - rand), pinsel);
        }

        using var daten = bild.Encode(format, 90);
        return daten.ToArray();
    }

    /// <summary>ICO-Datei mit einem eingebetteten PNG (Format seit Windows Vista).</summary>
    public static byte[] Ico(byte[] png, int kante)
    {
        using var ziel = new MemoryStream();
        using var schreiber = new BinaryWriter(ziel);
        schreiber.Write((ushort)0);
        schreiber.Write((ushort)1);
        schreiber.Write((ushort)1);
        schreiber.Write((byte)(kante >= 256 ? 0 : kante));
        schreiber.Write((byte)(kante >= 256 ? 0 : kante));
        schreiber.Write((byte)0);
        schreiber.Write((byte)0);
        schreiber.Write((ushort)1);
        schreiber.Write((ushort)32);
        schreiber.Write(png.Length);
        schreiber.Write(6 + 16);
        schreiber.Write(png);
        schreiber.Flush();
        return ziel.ToArray();
    }

    /// <summary>A4-Seite mit rotem Rechteck (100×60 pt) in der linken oberen Ecke, sonst weiß.</summary>
    public static byte[] PdfMitRotemRechteck()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        return Document.Create(dokument => dokument.Page(seite =>
        {
            seite.Size(PageSizes.A4);
            seite.Margin(0);
            seite.PageColor(Colors.White);
            seite.Content().AlignLeft().AlignTop().Width(100).Height(60).Background("#FF0000");
        })).GeneratePdf();
    }

    public static SKBitmap Lesen(byte[] png) => SKBitmap.Decode(png);
}
