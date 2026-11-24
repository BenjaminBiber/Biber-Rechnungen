using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Vorschau;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class VorschauTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Schritt_legt_Vorschau_als_Datei_und_BelegDatei_ab_und_zweiter_Lauf_ersetzt()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());
        var staging = Path.Combine(fabrik.TempOrdner, "staging", $"{beleg.Id}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(staging)!);
        await File.WriteAllBytesAsync(staging, TestPdf(2));

        await SchrittAusfuehrenAsync(beleg, staging);
        await SchrittAusfuehrenAsync(beleg, staging);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var datei = await db.BelegDateien.SingleAsync(d => d.BelegId == beleg.Id);
        datei.Rolle.ShouldBe(DateiRolle.Vorschau);
        datei.MimeType.ShouldBe("image/webp");
        datei.Pfad.ShouldBe(Path.Combine("vorschau", "2026", "01", $"{beleg.Id}-1-600.webp"));
        var bytes = await File.ReadAllBytesAsync(Path.Combine(fabrik.TempOrdner, "daten", datei.Pfad));
        datei.Groesse.ShouldBe(bytes.Length);
        IstWebp(bytes).ShouldBeTrue();
        using var bild = SKBitmap.Decode(bytes);
        bild.Width.ShouldBe(600);
    }

    [Fact]
    public async Task Schritt_ohne_lesbare_Quelle_geht_weiter_ohne_Vorschau()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());
        var kaputt = Path.Combine(fabrik.TempOrdner, "staging", $"{beleg.Id}.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(kaputt)!);
        await File.WriteAllTextAsync(kaputt, "%PDF-1.7 kein echtes PDF");

        await SchrittAusfuehrenAsync(beleg, kaputt);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.BelegDateien.AnyAsync(d => d.BelegId == beleg.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Endpunkt_liefert_WebP_mit_Cache_Headern_und_zweiter_Abruf_kommt_aus_dem_Zwischenspeicher()
    {
        var beleg = await BelegMitOriginalAsync(TestPdf(3), "pdf");
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        var antwort = await client.GetAsync($"/api/belege/{beleg.Id}/vorschau?seite=2&breite=400");

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        antwort.Headers.CacheControl!.Private.ShouldBeTrue();
        antwort.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(1));
        var etag = antwort.Headers.ETag.ShouldNotBeNull();
        var bytes = await antwort.Content.ReadAsByteArrayAsync();
        IstWebp(bytes).ShouldBeTrue();
        using (var bild = SKBitmap.Decode(bytes))
        {
            bild.Width.ShouldBe(400);
        }

        var cache = Path.Combine(fabrik.TempOrdner, "daten", "vorschau", "2026", "01", $"{beleg.Id}-2-400.webp");
        var zeitstempel = File.GetLastWriteTimeUtc(cache);

        var zweite = await client.GetAsync($"/api/belege/{beleg.Id}/vorschau?seite=2&breite=400");

        zweite.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await zweite.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        File.GetLastWriteTimeUtc(cache).ShouldBe(zeitstempel);

        using var bedingt = new HttpRequestMessage(HttpMethod.Get, $"/api/belege/{beleg.Id}/vorschau?seite=2&breite=400");
        bedingt.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag.Tag));
        (await client.SendAsync(bedingt)).StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Breite_wird_auf_200_bis_1600_begrenzt()
    {
        var beleg = await BelegMitOriginalAsync(TestPdf(1), "pdf");
        var client = fabrik.ErzeugeClient();

        using var klein = SKBitmap.Decode(await client.GetByteArrayAsync($"/api/belege/{beleg.Id}/vorschau?breite=10"));
        using var gross = SKBitmap.Decode(await client.GetByteArrayAsync($"/api/belege/{beleg.Id}/vorschau?breite=5000"));

        klein.Width.ShouldBe(200);
        gross.Width.ShouldBe(1600);
    }

    [Fact]
    public async Task PNG_Beleg_liefert_eine_Vorschau()
    {
        var beleg = await BelegMitOriginalAsync(TestPng(), "png");

        var antwort = await fabrik.ErzeugeClient().GetAsync($"/api/belege/{beleg.Id}/vorschau?breite=300");

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        using var bild = SKBitmap.Decode(await antwort.Content.ReadAsByteArrayAsync());
        bild.Width.ShouldBe(300);
        bild.Height.ShouldBe(150);
    }

    [Fact]
    public async Task Schritt_legt_Vorschau_fuer_TIFF_an()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());
        var staging = Path.Combine(fabrik.TempOrdner, "staging", $"{beleg.Id}.tif");
        Directory.CreateDirectory(Path.GetDirectoryName(staging)!);
        await File.WriteAllBytesAsync(staging, await File.ReadAllBytesAsync(TiffPfad("einseitig.tif")));

        await SchrittAusfuehrenAsync(beleg, staging);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var datei = await db.BelegDateien.SingleAsync(d => d.BelegId == beleg.Id);
        datei.Rolle.ShouldBe(DateiRolle.Vorschau);
        datei.MimeType.ShouldBe("image/webp");
        using var bild = SKBitmap.Decode(await File.ReadAllBytesAsync(Path.Combine(fabrik.TempOrdner, "daten", datei.Pfad)));
        bild.Width.ShouldBe(600);
    }

    [Fact]
    public async Task TIFF_Beleg_liefert_eine_Vorschau_und_Original_bleibt_unveraendert()
    {
        var inhalt = await File.ReadAllBytesAsync(TiffPfad("einseitig.tif"));
        var beleg = await BelegMitOriginalAsync(inhalt, "tif");
        var original = Path.Combine(fabrik.TempOrdner, "ablage", "2026", "01", $"{beleg.Id}.tif");
        var hashVorher = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(original)));

        var antwort = await fabrik.ErzeugeClient().GetAsync($"/api/belege/{beleg.Id}/vorschau?breite=300");

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        using var bild = SKBitmap.Decode(await antwort.Content.ReadAsByteArrayAsync());
        bild.Width.ShouldBe(300);
        bild.Height.ShouldBe(150);
        Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(original))).ShouldBe(hashVorher);
    }

    [Fact]
    public async Task Mehrseitiges_TIFF_liefert_Seite_2_und_404_fuer_ungueltige_Seite()
    {
        var beleg = await BelegMitOriginalAsync(await File.ReadAllBytesAsync(TiffPfad("mehrseitig.tif")), "tif");
        var client = fabrik.ErzeugeClient();

        var antwort = await client.GetAsync($"/api/belege/{beleg.Id}/vorschau?seite=2&breite=400");

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var bild = SKBitmap.Decode(await antwort.Content.ReadAsByteArrayAsync());
        bild.Width.ShouldBe(400);
        bild.Height.ShouldBe(400); // Seite 2 ist quadratisch und grün, Seiten 1 und 3 sind 2:1
        var mitte = bild.GetPixel(200, 200);
        mitte.Green.ShouldBeGreaterThan((byte)200);
        mitte.Red.ShouldBeLessThan((byte)50);
        (await client.GetAsync($"/api/belege/{beleg.Id}/vorschau?seite=0")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/belege/{beleg.Id}/vorschau?seite=4")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task XML_Beleg_liefert_404()
    {
        var beleg = await BelegMitOriginalAsync("<Invoice/>"u8.ToArray(), "xml");

        var antwort = await fabrik.ErzeugeClient().GetAsync($"/api/belege/{beleg.Id}/vorschau");

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unbekannter_Beleg_oder_fehlende_Seite_liefert_404()
    {
        var beleg = await BelegMitOriginalAsync(TestPdf(1), "pdf");
        var client = fabrik.ErzeugeClient();

        (await client.GetAsync($"/api/belege/{Guid.NewGuid()}/vorschau")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/belege/{beleg.Id}/vorschau?seite=2")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/belege/{beleg.Id}/vorschau?seite=0")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonym_liefert_401()
    {
        var antwort = await fabrik.ErzeugeAnonymenClient().GetAsync($"/api/belege/{Guid.NewGuid()}/vorschau");

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task SchrittAusfuehrenAsync(Beleg beleg, string dateiPfad)
    {
        using var scope = fabrik.ErzeugeScope();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<VorschauSchritt>().Single();
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = new ImportVorgang { Id = Guid.NewGuid(), Dateiname = "rechnung.pdf", Status = ImportStatus.InVerarbeitung },
            Beleg = beleg,
            DateiPfad = dateiPfad,
            Dateiname = "rechnung.pdf",
        };

        (await schritt.AusfuehrenAsync(kontext, CancellationToken.None)).ShouldBe(SchrittErgebnis.Weiter);
        await scope.ServiceProvider.GetRequiredService<BiberDbContext>().SaveChangesAsync();
    }

    private async Task<Beleg> BelegMitOriginalAsync(byte[] inhalt, string endung)
    {
        var beleg = Testdaten.Beleg();
        var relativ = Path.Combine("2026", "01", $"{beleg.Id}.{endung}");
        var absolut = Path.Combine(fabrik.TempOrdner, "ablage", relativ);
        Directory.CreateDirectory(Path.GetDirectoryName(absolut)!);
        await File.WriteAllBytesAsync(absolut, inhalt);
        beleg.Dateien.Add(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Rolle = DateiRolle.Original,
            Pfad = relativ,
            Sha256 = new string('0', 64),
            Groesse = inhalt.Length,
            MimeType = "application/octet-stream",
            AbgelegtAm = DateTimeOffset.UnixEpoch,
        });
        return await fabrik.SpeichernAsync(beleg);
    }

    private static string TiffPfad(string name) => Path.Combine(AppContext.BaseDirectory, "Testdateien", "tiff", name);

    private static bool IstWebp(byte[] bytes) =>
        bytes.Length > 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);

    /// <summary>Minimales PDF mit <paramref name="seiten"/> leeren A4-Seiten (PDFsharp 6.5 verlangt zum Erzeugen eine Lizenz).</summary>
    private static byte[] TestPdf(int seiten)
    {
        var objekte = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(3, seiten).Select(n => $"{n} 0 R"))}] /Count {seiten} >>",
        };
        objekte.AddRange(Enumerable.Repeat("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>", seiten));

        var text = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objekte.Count; i++)
        {
            offsets.Add(text.Length);
            text.Append($"{i + 1} 0 obj\n{objekte[i]}\nendobj\n");
        }

        var xref = text.Length;
        text.Append($"xref\n0 {objekte.Count + 1}\n0000000000 65535 f \n");
        offsets.ForEach(o => text.Append($"{o:D10} 00000 n \n"));
        text.Append($"trailer\n<< /Size {objekte.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(text.ToString());
    }

    private static byte[] TestPng()
    {
        using var bitmap = new SKBitmap(600, 300);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var daten = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return daten.ToArray();
    }
}
