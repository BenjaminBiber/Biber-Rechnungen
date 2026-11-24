using System.Security.Cryptography;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace Biber.Invoice.Infrastructure.Module.Logos;

/// <summary>Logo-Datei mit ETag für den Abruf.</summary>
public sealed record LogoDatei(byte[] Inhalt, string ETag);

/// <summary>Logos der Korrespondenten aus Upload, PDF-Ausschnitt und Favicon.</summary>
public interface ILogoDienst
{
    Task<LogoDatei?> LesenAsync(Guid korrespondentId, CancellationToken ct);

    /// <returns><c>null</c>, wenn es den Korrespondenten nicht gibt.</returns>
    /// <exception cref="LogoAusnahme">Kein Rasterbild oder zu groß.</exception>
    Task<LogoDto?> HochladenAsync(Guid korrespondentId, byte[] inhalt, CancellationToken ct);

    /// <returns><c>false</c>, wenn es den Korrespondenten nicht gibt.</returns>
    Task<bool> LoeschenAsync(Guid korrespondentId, CancellationToken ct);

    /// <returns><c>null</c>, wenn es den Beleg nicht gibt.</returns>
    /// <exception cref="LogoAusnahme">Ausschnitt ungültig (400), Beleg ohne Korrespondent oder ohne Seitenbild (409).</exception>
    Task<LogoDto?> AusBelegAsync(Guid belegId, LogoAusschnittDto ausschnitt, CancellationToken ct);

    /// <summary>Ruft das Favicon ab und setzt es auch gegen die Rangfolge.</summary>
    /// <returns><c>null</c>, wenn es den Korrespondenten nicht gibt.</returns>
    /// <exception cref="LogoAusnahme">Keine Domain oder kein Favicon gefunden (404).</exception>
    Task<LogoDto?> FaviconAsync(Guid korrespondentId, CancellationToken ct);

    /// <summary>
    /// Speichert ein Bild als Logo. Ohne <paramref name="erzwingen"/> gilt die Rangfolge Upload &gt; Pdf &gt; Favicon:
    /// ein vorhandenes Logo gleicher oder höherer Stufe bleibt.
    /// </summary>
    /// <returns><c>false</c>, wenn das Bild nicht lesbar ist oder die Rangfolge das Setzen verhindert.</returns>
    Task<bool> SetzenAsync(Korrespondent korrespondent, byte[] bild, LogoQuelle quelle, bool erzwingen, CancellationToken ct);
}

/// <summary>
/// Legt Logos als PNG (höchstens 256×256, transparenter Rand entfernt) unter <c>Pfade:Daten/logos/{KorrespondentId}.png</c> ab
/// und pflegt <see cref="Korrespondent.LogoPfad"/> und <see cref="Korrespondent.LogoQuelle"/>.
/// </summary>
public sealed class LogoDienst(
    BiberDbContext db,
    IPdfRenderer renderer,
    IFaviconAbruf favicons,
    IConfiguration konfiguration) : ILogoDienst
{
    public const int MaxUploadGroesse = 2 * 1024 * 1024;
    public const int RenderBreite = 1600;
    public const int MinAusschnittPx = 16;

    public async Task<LogoDatei?> LesenAsync(Guid korrespondentId, CancellationToken ct)
    {
        var pfad = await db.Korrespondenten.AsNoTracking()
            .Where(k => k.Id == korrespondentId)
            .Select(k => k.LogoPfad)
            .FirstOrDefaultAsync(ct);
        if (pfad is null)
        {
            return null;
        }

        var datei = Path.Combine(Daten, pfad);
        if (!File.Exists(datei))
        {
            return null;
        }

        var inhalt = await File.ReadAllBytesAsync(datei, ct);
        return new LogoDatei(inhalt, $"\"{Convert.ToHexStringLower(SHA256.HashData(inhalt))[..32]}\"");
    }

    public async Task<LogoDto?> HochladenAsync(Guid korrespondentId, byte[] inhalt, CancellationToken ct)
    {
        var korrespondent = await db.Korrespondenten.FirstOrDefaultAsync(k => k.Id == korrespondentId, ct);
        if (korrespondent is null)
        {
            return null;
        }

        if (inhalt.Length > MaxUploadGroesse)
        {
            throw new LogoAusnahme(LogoAusnahme.Ungueltig, "Das Logo darf höchstens 2 MB groß sein.", "datei");
        }

        if (LogoBild.Format(inhalt) is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp)
            || !await SetzenAsync(korrespondent, inhalt, LogoQuelle.Upload, erzwingen: true, ct))
        {
            throw new LogoAusnahme(LogoAusnahme.Ungueltig, "Erlaubt sind nur PNG-, JPG- und WebP-Bilder.", "datei");
        }

        return Dto(korrespondent);
    }

    public async Task<bool> LoeschenAsync(Guid korrespondentId, CancellationToken ct)
    {
        var korrespondent = await db.Korrespondenten.FirstOrDefaultAsync(k => k.Id == korrespondentId, ct);
        if (korrespondent is null)
        {
            return false;
        }

        if (korrespondent.LogoPfad is { } pfad && File.Exists(Path.Combine(Daten, pfad)))
        {
            File.Delete(Path.Combine(Daten, pfad));
        }

        korrespondent.LogoPfad = null;
        korrespondent.LogoQuelle = LogoQuelle.Keine;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LogoDto?> AusBelegAsync(Guid belegId, LogoAusschnittDto ausschnitt, CancellationToken ct)
    {
        var beleg = await db.Belege.AsNoTracking()
            .Where(b => b.Id == belegId)
            .Select(b => new { b.KorrespondentId })
            .FirstOrDefaultAsync(ct);
        if (beleg is null)
        {
            return null;
        }

        PruefeAusschnitt(ausschnitt);
        var korrespondent = beleg.KorrespondentId is { } id
            ? await db.Korrespondenten.FirstOrDefaultAsync(k => k.Id == id, ct)
            : null;
        if (korrespondent is null)
        {
            throw new LogoAusnahme(LogoAusnahme.Konflikt, "Der Beleg hat keinen Korrespondenten.");
        }

        using var seite = await SeitenbildAsync(belegId, ausschnitt.Seite, ct);
        var rahmen = SKRectI.Create(
            (int)Math.Floor(ausschnitt.X * seite.Width),
            (int)Math.Floor(ausschnitt.Y * seite.Height),
            (int)Math.Round(ausschnitt.Breite * seite.Width),
            (int)Math.Round(ausschnitt.Hoehe * seite.Height));
        rahmen.Intersect(new SKRectI(0, 0, seite.Width, seite.Height));
        if (rahmen.Width < MinAusschnittPx || rahmen.Height < MinAusschnittPx)
        {
            throw new LogoAusnahme(LogoAusnahme.Ungueltig, $"Der Ausschnitt muss mindestens {MinAusschnittPx}×{MinAusschnittPx} Pixel groß sein.", "breite");
        }

        using var teil = new SKBitmap();
        if (!seite.ExtractSubset(teil, rahmen))
        {
            throw new LogoAusnahme(LogoAusnahme.Ungueltig, "Der Ausschnitt liegt außerhalb der Seite.", "x");
        }

        await SpeichernAsync(korrespondent, LogoBild.AlsLogoPng(teil), LogoQuelle.Pdf, ct);
        return Dto(korrespondent);
    }

    public async Task<LogoDto?> FaviconAsync(Guid korrespondentId, CancellationToken ct)
    {
        var korrespondent = await db.Korrespondenten.FirstOrDefaultAsync(k => k.Id == korrespondentId, ct);
        if (korrespondent is null)
        {
            return null;
        }

        var bild = string.IsNullOrWhiteSpace(korrespondent.Domain)
            ? null
            : await favicons.AbrufenAsync(korrespondent.Domain, ct);
        if (bild is null || !await SetzenAsync(korrespondent, bild, LogoQuelle.Favicon, erzwingen: true, ct))
        {
            throw new LogoAusnahme(LogoAusnahme.NichtGefunden, "Es wurde kein Favicon gefunden.");
        }

        return Dto(korrespondent);
    }

    public async Task<bool> SetzenAsync(Korrespondent korrespondent, byte[] bild, LogoQuelle quelle, bool erzwingen, CancellationToken ct)
    {
        if (!erzwingen && korrespondent.LogoPfad is not null && Rang(korrespondent.LogoQuelle) >= Rang(quelle))
        {
            return false;
        }

        var png = LogoBild.AlsLogoPng(bild);
        if (png is null)
        {
            return false;
        }

        await SpeichernAsync(korrespondent, png, quelle, ct);
        return true;
    }

    /// <summary>Rangfolge der Quellen: Upload &gt; Pdf &gt; Favicon &gt; Keine.</summary>
    public static int Rang(LogoQuelle quelle) => quelle switch
    {
        LogoQuelle.Upload => 3,
        LogoQuelle.Pdf => 2,
        LogoQuelle.Favicon => 1,
        _ => 0,
    };

    public static LogoDto Dto(Korrespondent k) => new(
        k.LogoPfad is null ? null : ApiRouten.Korrespondenten.Logo.Replace("{id}", k.Id.ToString(), StringComparison.Ordinal),
        Initialen(k.Name),
        k.LogoPfad is null ? LogoQuelle.Keine : k.LogoQuelle);

    private static string Initialen(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsLetterOrDigit(w[0]))
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0])));

    private async Task SpeichernAsync(Korrespondent korrespondent, byte[] png, LogoQuelle quelle, CancellationToken ct)
    {
        var relativ = Path.Combine("logos", $"{korrespondent.Id}.png");
        var ziel = Path.Combine(Daten, relativ);
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        await File.WriteAllBytesAsync(ziel, png, ct);

        korrespondent.LogoPfad = relativ.Replace('\\', '/');
        korrespondent.LogoQuelle = quelle;
        await db.SaveChangesAsync(ct);
    }

    private static void PruefeAusschnitt(LogoAusschnittDto a)
    {
        const double toleranz = 1e-6;
        if (a.Seite < 1)
        {
            throw new LogoAusnahme(LogoAusnahme.Ungueltig, "Die Seite muss mindestens 1 sein.", "seite");
        }

        if (a.X < 0 || a.Y < 0 || a.Breite <= 0 || a.Hoehe <= 0
            || a.X + a.Breite > 1 + toleranz || a.Y + a.Hoehe > 1 + toleranz)
        {
            throw new LogoAusnahme(LogoAusnahme.Ungueltig, "Der Ausschnitt liegt außerhalb der Seite.", "x");
        }
    }

    /// <summary>Seite als Bild: PDFs mit <see cref="RenderBreite"/> Pixeln gerendert, Bild-Belege als das Bild selbst.</summary>
    private async Task<SKBitmap> SeitenbildAsync(Guid belegId, int seite, CancellationToken ct)
    {
        var pfad = await db.BelegDateien.AsNoTracking()
            .Where(d => d.BelegId == belegId && d.Rolle == DateiRolle.Original)
            .Select(d => d.Pfad)
            .FirstOrDefaultAsync(ct);
        var datei = pfad is null ? null : Path.Combine(Ablage, pfad);
        if (datei is null || !File.Exists(datei))
        {
            throw new LogoAusnahme(LogoAusnahme.Konflikt, "Zum Beleg gibt es kein Seitenbild.");
        }

        var inhalt = await File.ReadAllBytesAsync(datei, ct);
        if (IstPdf(inhalt))
        {
            using var pdf = new MemoryStream(inhalt, writable: false);
            if (seite > renderer.Seitenanzahl(pdf))
            {
                throw new LogoAusnahme(LogoAusnahme.Ungueltig, "Die Seite gibt es im Beleg nicht.", "seite");
            }

            pdf.Position = 0;
            return LogoBild.Lesen(renderer.SeiteAlsPng(pdf, seite, RenderBreite))
                ?? throw new LogoAusnahme(LogoAusnahme.Konflikt, "Die Seite ließ sich nicht rendern.");
        }

        // Reines XML (E-Rechnung) oder Unbekanntes hat kein Seitenbild.
        var bild = LogoBild.Lesen(inhalt)
            ?? throw new LogoAusnahme(LogoAusnahme.Konflikt, "Der Beleg hat kein Seitenbild (z. B. E-Rechnung als reines XML).");
        if (seite != 1)
        {
            bild.Dispose();
            throw new LogoAusnahme(LogoAusnahme.Ungueltig, "Die Seite gibt es im Beleg nicht.", "seite");
        }

        return bild;
    }

    private static bool IstPdf(byte[] inhalt) =>
        inhalt.Length >= 5 && inhalt.AsSpan(0, Math.Min(inhalt.Length, 1024)).IndexOf("%PDF-"u8) >= 0;

    private string Daten => konfiguration["Pfade:Daten"]
        ?? throw new InvalidOperationException("Pfade:Daten ist nicht konfiguriert.");

    private string Ablage => konfiguration["Pfade:Ablage"]
        ?? throw new InvalidOperationException("Pfade:Ablage ist nicht konfiguriert.");
}
