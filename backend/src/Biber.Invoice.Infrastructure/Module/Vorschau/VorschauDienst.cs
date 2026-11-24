using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using BitMiracle.LibTiff.Classic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace Biber.Invoice.Infrastructure.Module.Vorschau;

/// <summary>
/// Rendert Seitenbilder aus dem Original (PDF über <see cref="IPdfRenderer"/>, TIFF auch mehrseitig über LibTiff, andere Bilder über SkiaSharp) und legt sie als WebP unter
/// <c>Pfade:Daten/vorschau/{yyyy}/{MM}/{BelegId}-{seite}-{breite}.webp</c> ab; ein vorhandenes Bild wird nicht neu gerendert.
/// </summary>
public sealed class VorschauDienst(IPdfRenderer renderer, BiberDbContext db, IConfiguration konfiguration) : IVorschauDienst
{
    private const int WebpQualitaet = 80;

    public async Task<byte[]?> SeiteAsync(Beleg beleg, int seite, int breitePx, CancellationToken ct, string? quellPfad = null)
    {
        var ziel = Path.Combine(Daten, RelativerPfad(beleg, seite, breitePx));
        if (File.Exists(ziel))
        {
            return await File.ReadAllBytesAsync(ziel, ct);
        }

        quellPfad ??= await OriginalPfadAsync(beleg.Id, ct);
        if (quellPfad is null || !File.Exists(quellPfad))
        {
            return null;
        }

        var quelle = await File.ReadAllBytesAsync(quellPfad, ct);
        var webp = Rendern(quelle, seite, breitePx);
        if (webp is null)
        {
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        await File.WriteAllBytesAsync(ziel, webp, ct);
        return webp;
    }

    public void Verwerfen(Beleg beleg, int seite, int breitePx)
    {
        var pfad = Path.Combine(Daten, RelativerPfad(beleg, seite, breitePx));
        if (File.Exists(pfad))
        {
            File.Delete(pfad);
        }
    }

    public string RelativerPfad(Beleg beleg, int seite, int breitePx) =>
        Path.Combine("vorschau", beleg.ErstelltAm.UtcDateTime.ToString("yyyy"), beleg.ErstelltAm.UtcDateTime.ToString("MM"), $"{beleg.Id}-{seite}-{breitePx}.webp");

    private string Daten => konfiguration["Pfade:Daten"]
        ?? throw new InvalidOperationException("Pfade:Daten ist nicht konfiguriert.");

    private async Task<string?> OriginalPfadAsync(Guid belegId, CancellationToken ct)
    {
        var pfad = await db.BelegDateien.AsNoTracking()
            .Where(d => d.BelegId == belegId && d.Rolle == DateiRolle.Original)
            .Select(d => d.Pfad)
            .FirstOrDefaultAsync(ct);
        if (pfad is null)
        {
            return null;
        }

        var ablage = konfiguration["Pfade:Ablage"]
            ?? throw new InvalidOperationException("Pfade:Ablage ist nicht konfiguriert.");
        return Path.Combine(ablage, pfad);
    }

    private byte[]? Rendern(byte[] quelle, int seite, int breitePx)
    {
        if (IstPdf(quelle))
        {
            using var pdf = new MemoryStream(quelle, writable: false);
            if (seite > renderer.Seitenanzahl(pdf))
            {
                return null;
            }

            pdf.Position = 0;
            using var png = SKBitmap.Decode(renderer.SeiteAlsPng(pdf, seite, breitePx));
            return AlsWebp(png);
        }

        if (IstTiff(quelle))
        {
            using var tiffBild = TiffSeite(quelle, seite);
            return tiffBild is null ? null : SkaliertAlsWebp(tiffBild, breitePx);
        }

        // Bilder haben genau eine Seite; reines XML oder Unbekanntes kann SkiaSharp nicht lesen.
        if (seite != 1)
        {
            return null;
        }

        using var codec = SKCodec.Create(new MemoryStream(quelle, writable: false));
        using var bild = codec is null ? null : SKBitmap.Decode(codec);
        return bild is null ? null : SkaliertAlsWebp(bild, breitePx);
    }

    /// <summary>Dekodiert Seite <paramref name="seite"/> (1-basiert) eines TIFF mit LibTiff; null, wenn es die Seite nicht gibt.</summary>
    private static SKBitmap? TiffSeite(byte[] quelle, int seite)
    {
        using var tiff = Tiff.ClientOpen("vorschau", "r", new MemoryStream(quelle, writable: false), new TiffStream());
        if (tiff is null || seite < 1 || seite > tiff.NumberOfDirectories() || !tiff.SetDirectory((short)(seite - 1)))
        {
            return null;
        }

        var breite = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        var hoehe = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        var raster = new int[breite * hoehe];
        if (!tiff.ReadRGBAImageOriented(breite, hoehe, raster, Orientation.TOPLEFT))
        {
            return null;
        }

        // LibTiff packt je Pixel 0xAABBGGRR in ein int; Kanäle einzeln auslesen, damit die Bytefolge nicht von der Plattform abhängt.
        var bitmap = new SKBitmap(new SKImageInfo(breite, hoehe, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var ziel = bitmap.GetPixelSpan();
        for (var i = 0; i < raster.Length; i++)
        {
            var p = raster[i];
            ziel[i * 4] = (byte)Tiff.GetR(p);
            ziel[(i * 4) + 1] = (byte)Tiff.GetG(p);
            ziel[(i * 4) + 2] = (byte)Tiff.GetB(p);
            ziel[(i * 4) + 3] = (byte)Tiff.GetA(p);
        }

        return bitmap;
    }

    private static byte[] SkaliertAlsWebp(SKBitmap bild, int breitePx)
    {
        var hoehe = Math.Max(1, (int)Math.Round(bild.Height * (double)breitePx / bild.Width));
        using var skaliert = bild.Resize(new SKImageInfo(breitePx, hoehe), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        return AlsWebp(skaliert);
    }

    private static bool IstTiff(byte[] quelle) =>
        quelle.Length >= 4 && (quelle.AsSpan(0, 4).SequenceEqual("II*\0"u8) || quelle.AsSpan(0, 4).SequenceEqual("MM\0*"u8));

    private static byte[] AlsWebp(SKBitmap bitmap)
    {
        using var daten = bitmap.Encode(SKEncodedImageFormat.Webp, WebpQualitaet);
        return daten.ToArray();
    }

    private static bool IstPdf(byte[] quelle) =>
        quelle.Length >= 5 && quelle.AsSpan(0, Math.Min(quelle.Length, 1024)).IndexOf("%PDF-"u8) >= 0;
}
