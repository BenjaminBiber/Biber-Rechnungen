using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Logos;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SkiaSharp;

namespace Biber.Invoice.IntegrationTests.Logos;

[Collection("Api")]
public sealed class LogoTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Upload_einer_PNG_ist_abrufbar_verkleinert_und_hat_Quelle_Upload()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Bürowelt Krämer"));
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PutAsync(Route(k.Id), Formular(LogoTestbilder.Png(800, 400, SKColors.Blue, rand: 50)));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await antwort.Content.ReadFromJsonAsync<LogoDto>())!;
        dto.Quelle.ShouldBe(LogoQuelle.Upload);
        dto.Url.ShouldBe(Route(k.Id));

        var abruf = await client.GetAsync(Route(k.Id));
        abruf.StatusCode.ShouldBe(HttpStatusCode.OK);
        abruf.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        abruf.Headers.ETag.ShouldNotBeNull();
        abruf.Headers.CacheControl!.Private.ShouldBeTrue();
        abruf.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromHours(1));
        using var bild = LogoTestbilder.Lesen(await abruf.Content.ReadAsByteArrayAsync());
        // 700×300 ohne transparenten Rand, auf 256 px Kante verkleinert.
        bild.Width.ShouldBe(256);
        bild.Height.ShouldBe(110);

        var gespeichert = await KorrespondentAsync(k.Id);
        gespeichert.LogoQuelle.ShouldBe(LogoQuelle.Upload);
        File.Exists(Path.Combine(fabrik.TempOrdner, "daten", "logos", $"{k.Id}.png")).ShouldBeTrue();
    }

    [Fact]
    public async Task Gleiches_ETag_ergibt_304()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var client = fabrik.ErzeugeClient();
        (await client.PutAsync(Route(k.Id), Formular(LogoTestbilder.Png(32, 32, SKColors.Green)))).EnsureSuccessStatusCode();
        var etag = (await client.GetAsync(Route(k.Id))).Headers.ETag!;

        using var anfrage = new HttpRequestMessage(HttpMethod.Get, Route(k.Id));
        anfrage.Headers.IfNoneMatch.Add(etag);
        (await client.SendAsync(anfrage)).StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task Textdatei_mit_Endung_png_ergibt_400()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());

        var antwort = await fabrik.ErzeugeClient().PutAsync(Route(k.Id), Formular("Kein Bild, nur Text."u8.ToArray()));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await KorrespondentAsync(k.Id)).LogoPfad.ShouldBeNull();
    }

    [Fact]
    public async Task SVG_ergibt_400()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><rect width="10" height="10"/></svg>"""u8.ToArray();

        var antwort = await fabrik.ErzeugeClient().PutAsync(Route(k.Id), Formular(svg, "logo.svg"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Zu_grosse_Datei_ergibt_400()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var gross = new byte[LogoDienst.MaxUploadGroesse + 1];
        LogoTestbilder.Png(16, 16, SKColors.Red).CopyTo(gross, 0);

        var antwort = await fabrik.ErzeugeClient().PutAsync(Route(k.Id), Formular(gross));

        antwort.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);
        (await KorrespondentAsync(k.Id)).LogoPfad.ShouldBeNull();
    }

    [Fact]
    public async Task Ohne_Logo_ergibt_404()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());

        (await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync(Route(k.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Loeschen_entfernt_Datei_und_Quelle()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var client = fabrik.ErzeugeClient();
        (await client.PutAsync(Route(k.Id), Formular(LogoTestbilder.Png(64, 64, SKColors.Red)))).EnsureSuccessStatusCode();
        var datei = Path.Combine(fabrik.TempOrdner, "daten", "logos", $"{k.Id}.png");
        File.Exists(datei).ShouldBeTrue();

        (await client.DeleteAsync(Route(k.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        File.Exists(datei).ShouldBeFalse();
        var gespeichert = await KorrespondentAsync(k.Id);
        gespeichert.LogoPfad.ShouldBeNull();
        gespeichert.LogoQuelle.ShouldBe(LogoQuelle.Keine);
        (await client.GetAsync(Route(k.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ausschnitt_aus_PDF_hat_die_Farbe_des_Rechtecks_oben_links()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var belegId = await BelegMitDateiAsync(k.Id, LogoTestbilder.PdfMitRotemRechteck(), "pdf");

        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer)
            .PostAsJsonAsync(UebernehmenRoute(belegId), new LogoAusschnittDto(1, 0.02, 0.01, 0.12, 0.05));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.ReadFromJsonAsync<LogoDto>())!.Quelle.ShouldBe(LogoQuelle.Pdf);
        var gespeichert = await KorrespondentAsync(k.Id);
        gespeichert.LogoQuelle.ShouldBe(LogoQuelle.Pdf);
        using var bild = LogoTestbilder.Lesen(await File.ReadAllBytesAsync(Path.Combine(fabrik.TempOrdner, "daten", gespeichert.LogoPfad!)));
        var mitte = bild.GetPixel(bild.Width / 2, bild.Height / 2);
        mitte.Red.ShouldBeGreaterThan((byte)200);
        mitte.Green.ShouldBeLessThan((byte)60);
        mitte.Blue.ShouldBeLessThan((byte)60);
    }

    [Fact]
    public async Task Ausschnitt_aus_Bild_Beleg_nimmt_das_Bild_selbst()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var belegId = await BelegMitDateiAsync(k.Id, LogoTestbilder.Png(400, 400, SKColors.Lime), "png");

        var antwort = await fabrik.ErzeugeClient()
            .PostAsJsonAsync(UebernehmenRoute(belegId), new LogoAusschnittDto(1, 0.25, 0.25, 0.5, 0.5));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var bild = LogoTestbilder.Lesen(await (await fabrik.ErzeugeClient().GetAsync(Route(k.Id))).Content.ReadAsByteArrayAsync());
        bild.Width.ShouldBe(200);
        bild.GetPixel(100, 100).Green.ShouldBeGreaterThan((byte)200);
    }

    public static TheoryData<LogoAusschnittDto> UngueltigeAusschnitte() => new()
    {
        new LogoAusschnittDto(1, 0.9, 0.1, 0.2, 0.1),
        new LogoAusschnittDto(1, -0.1, 0.1, 0.2, 0.1),
        new LogoAusschnittDto(1, 0.1, 0.1, 0.001, 0.001),
        new LogoAusschnittDto(2, 0.1, 0.1, 0.2, 0.1),
        new LogoAusschnittDto(0, 0.1, 0.1, 0.2, 0.1),
    };

    [Theory]
    [MemberData(nameof(UngueltigeAusschnitte))]
    public async Task Ausschnitt_ausserhalb_oder_zu_klein_ergibt_400(LogoAusschnittDto ausschnitt)
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var belegId = await BelegMitDateiAsync(k.Id, LogoTestbilder.PdfMitRotemRechteck(), "pdf");

        var antwort = await fabrik.ErzeugeClient().PostAsJsonAsync(UebernehmenRoute(belegId), ausschnitt);

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await KorrespondentAsync(k.Id)).LogoPfad.ShouldBeNull();
    }

    [Fact]
    public async Task Beleg_ohne_Korrespondent_ergibt_409()
    {
        var belegId = await BelegMitDateiAsync(null, LogoTestbilder.PdfMitRotemRechteck(), "pdf");

        var antwort = await fabrik.ErzeugeClient().PostAsJsonAsync(UebernehmenRoute(belegId), new LogoAusschnittDto(1, 0, 0, 0.2, 0.1));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task E_Rechnung_als_reines_XML_ergibt_409()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var belegId = await BelegMitDateiAsync(k.Id, "<?xml version=\"1.0\"?><Invoice/>"u8.ToArray(), "xml");

        var antwort = await fabrik.ErzeugeClient().PostAsJsonAsync(UebernehmenRoute(belegId), new LogoAusschnittDto(1, 0, 0, 0.2, 0.1));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unbekannter_Beleg_ergibt_404()
    {
        var antwort = await fabrik.ErzeugeClient().PostAsJsonAsync(UebernehmenRoute(Guid.NewGuid()), new LogoAusschnittDto(1, 0, 0, 0.2, 0.1));

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Favicon_Endpunkt_setzt_auch_gegen_die_Rangfolge()
    {
        var favicons = Substitute.For<IFaviconAbruf>();
        favicons.AbrufenAsync("beispiel.de", Arg.Any<CancellationToken>()).Returns(LogoTestbilder.Png(32, 32, SKColors.Orange));
        var f = fabrik.MitDiensten(d => d.AddSingleton(favicons));
        var k = Testdaten.Korrespondent();
        k.Domain = "beispiel.de";
        await f.SpeichernAsync(k);
        var client = f.ErzeugeClient();
        (await client.PutAsync(Route(k.Id), Formular(LogoTestbilder.Png(64, 64, SKColors.Red)))).EnsureSuccessStatusCode();

        var antwort = await client.PostAsync(FaviconRoute(k.Id), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.ReadFromJsonAsync<LogoDto>())!.Quelle.ShouldBe(LogoQuelle.Favicon);
        (await KorrespondentAsync(k.Id)).LogoQuelle.ShouldBe(LogoQuelle.Favicon);
    }

    [Fact]
    public async Task Favicon_Endpunkt_ohne_Favicon_ergibt_404()
    {
        var favicons = Substitute.For<IFaviconAbruf>();
        favicons.AbrufenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((byte[]?)null);
        var f = fabrik.MitDiensten(d => d.AddSingleton(favicons));
        var k = Testdaten.Korrespondent();
        k.Domain = "ohne-icon.de";
        await f.SpeichernAsync(k);

        (await f.ErzeugeClient().PostAsync(FaviconRoute(k.Id), null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rangfolge_Upload_vor_Pdf_vor_Favicon_fuer_automatisches_Setzen()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var logos = scope.ServiceProvider.GetRequiredService<ILogoDienst>();
        var korrespondent = await db.Korrespondenten.SingleAsync(x => x.Id == k.Id);
        var bild = LogoTestbilder.Png(32, 32, SKColors.Black);

        (await logos.SetzenAsync(korrespondent, bild, LogoQuelle.Favicon, erzwingen: false, default)).ShouldBeTrue();
        (await logos.SetzenAsync(korrespondent, bild, LogoQuelle.Favicon, erzwingen: false, default)).ShouldBeFalse();
        (await logos.SetzenAsync(korrespondent, bild, LogoQuelle.Pdf, erzwingen: false, default)).ShouldBeTrue();
        (await logos.SetzenAsync(korrespondent, bild, LogoQuelle.Favicon, erzwingen: false, default)).ShouldBeFalse();
        (await logos.SetzenAsync(korrespondent, bild, LogoQuelle.Upload, erzwingen: false, default)).ShouldBeTrue();
        (await logos.SetzenAsync(korrespondent, bild, LogoQuelle.Pdf, erzwingen: false, default)).ShouldBeFalse();
        korrespondent.LogoQuelle.ShouldBe(LogoQuelle.Upload);

        // Die manuellen Wege überschreiben immer.
        (await logos.SetzenAsync(korrespondent, bild, LogoQuelle.Pdf, erzwingen: true, default)).ShouldBeTrue();
        korrespondent.LogoQuelle.ShouldBe(LogoQuelle.Pdf);
    }

    [Fact]
    public async Task NurLesen_ergibt_403_bei_Aenderungen()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await client.PutAsync(Route(k.Id), Formular(LogoTestbilder.Png(32, 32, SKColors.Red)))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync(Route(k.Id))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync(FaviconRoute(k.Id), null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync(UebernehmenRoute(Guid.NewGuid()), new LogoAusschnittDto(1, 0, 0, 0.2, 0.1)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static string Route(Guid id) => ApiRouten.Korrespondenten.Logo.Replace("{id}", id.ToString());

    private static string FaviconRoute(Guid id) => ApiRouten.Korrespondenten.LogoFavicon.Replace("{id}", id.ToString());

    private static string UebernehmenRoute(Guid id) => ApiRouten.Belege.LogoUebernehmen.Replace("{id}", id.ToString());

    private static MultipartFormDataContent Formular(byte[] inhalt, string dateiname = "logo.png")
    {
        var datei = new ByteArrayContent(inhalt);
        datei.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { datei, "datei", dateiname } };
    }

    private async Task<Korrespondent> KorrespondentAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Korrespondenten.AsNoTracking().SingleAsync(k => k.Id == id);
    }

    /// <summary>Beleg mit Original-Datei in der Ablage, ohne Pipeline.</summary>
    private async Task<Guid> BelegMitDateiAsync(Guid? korrespondentId, byte[] inhalt, string endung)
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(korrespondentId: korrespondentId));
        var relativ = $"logos-test/{Guid.NewGuid():N}.{endung}";
        var ziel = Path.Combine(fabrik.TempOrdner, "ablage", relativ);
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        await File.WriteAllBytesAsync(ziel, inhalt);
        await fabrik.SpeichernAsync(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Rolle = DateiRolle.Original,
            Pfad = relativ,
            Sha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            MimeType = endung == "xml" ? "application/xml" : endung == "pdf" ? "application/pdf" : "image/png",
            AbgelegtAm = fabrik.Zeit.GetUtcNow(),
        });
        return beleg.Id;
    }
}
