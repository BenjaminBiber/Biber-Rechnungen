using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Formaterkennung;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Import;

[Collection("Api")]
public sealed class ImportEndpunktTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private const string Anzeigename = "Berta Beispiel";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly ApiFabrik _f = OhneFormaterkennung(fabrik);

    public async Task InitializeAsync()
    {
        _f.Zeit.Setze(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await _f.ZuruecksetzenAsync();
        var benutzer = Testdaten.Benutzer(anzeigename: Anzeigename);
        benutzer.Id = ApiFabrik.BenutzerId(Rolle.Benutzer);
        await _f.SpeichernAsync(benutzer);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Upload_einer_PDF_legt_Vorgang_und_Beleg_an()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostAsync(ApiRouten.Import.Upload, Formular(("rechnung.pdf", Pdf())));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var vorgang = (await Lese<ImportVorgangDto[]>(antwort)).ShouldHaveSingleItem();
        vorgang.Quelle.ShouldBe(ImportQuelle.Upload);
        vorgang.Dateiname.ShouldBe("rechnung.pdf");
        vorgang.Herkunft.ShouldBe("rechnung.pdf");
        vorgang.HerkunftDetail.ShouldBe(Anzeigename);

        // Die Antwort zeigt den Stand bei der Annahme; die Verarbeitung lief danach im eigenen Scope.
        var gespeichert = await _f.VorgangAsync(vorgang.Id);
        gespeichert.BenutzerId.ShouldBe(ApiFabrik.BenutzerId(Rolle.Benutzer));
        gespeichert.BelegId.ShouldNotBeNull();
        using var scope = _f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var beleg = await db.Belege.AsNoTracking().SingleAsync(b => b.Id == gespeichert.BelegId);
        beleg.ImportVorgangId.ShouldBe(vorgang.Id);
        beleg.Quelle.ShouldBe(ImportQuelle.Upload);

        var detail = await Lese<ImportVorgangDto>(await client.GetAsync(ApiRouten.Import.Vorgang.Replace("{id}", vorgang.Id.ToString())));
        detail.BelegId.ShouldBe(beleg.Id);
        detail.Herkunft.ShouldBe("rechnung.pdf");
        detail.HerkunftDetail.ShouldBe(Anzeigename);
    }

    [Fact]
    public async Task Zwei_Dateien_ergeben_zwei_Vorgaenge_in_derselben_Reihenfolge()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostAsync(ApiRouten.Import.Upload, Formular(("zweite.pdf", Pdf()), ("erste.pdf", Pdf())));

        var vorgaenge = await Lese<ImportVorgangDto[]>(antwort);
        vorgaenge.Select(v => v.Dateiname).ShouldBe(["zweite.pdf", "erste.pdf"]);
        vorgaenge.Select(v => v.Id).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Nicht_unterstuetztes_Format_steht_als_Abgelehnt_in_der_Antwort()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostAsync(ApiRouten.Import.Upload, Formular(("foto.heic", Pdf()), ("rechnung.pdf", Pdf())));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var vorgaenge = await Lese<ImportVorgangDto[]>(antwort);
        vorgaenge.Length.ShouldBe(2);
        vorgaenge[0].Status.ShouldBe(ImportStatus.Abgelehnt);
        vorgaenge[0].Fehler.ShouldNotBeNullOrWhiteSpace();
        vorgaenge[1].Status.ShouldNotBe(ImportStatus.Abgelehnt);
    }

    [Fact]
    public async Task Upload_ohne_Datei_liefert_400()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);

        var leer = await client.PostAsync(ApiRouten.Import.Upload, new MultipartFormDataContent());
        var keinFormular = await client.PostAsJsonAsync(ApiRouten.Import.Upload, new { });

        leer.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        keinFormular.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Mehr_als_20_Dateien_liefern_400_und_legen_nichts_an()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);
        var dateien = Enumerable.Range(1, 21).Select(i => ($"datei{i}.pdf", Pdf())).ToArray();

        var antwort = await client.PostAsync(ApiRouten.Import.Upload, Formular(dateien));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Anzahl()).ShouldBe(0);
    }

    [Fact]
    public async Task Genau_20_Dateien_sind_erlaubt()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);
        var dateien = Enumerable.Range(1, 20).Select(i => ($"datei{i}.pdf", Pdf())).ToArray();

        var antwort = await client.PostAsync(ApiRouten.Import.Upload, Formular(dateien));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await Lese<ImportVorgangDto[]>(antwort)).Length.ShouldBe(20);
    }

    [Fact]
    public async Task Datei_ueber_50_MB_liefert_413()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);
        var zuGross = new byte[(50 * 1024 * 1024) + 1];

        var antwort = await client.PostAsync(ApiRouten.Import.Upload, Formular(("gross.pdf", zuGross)));

        antwort.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await Anzahl()).ShouldBe(0);
    }

    [Fact]
    public async Task Nur_lesen_darf_nicht_hochladen()
    {
        var client = _f.ErzeugeClient(Rolle.NurLesen);

        var antwort = await client.PostAsync(ApiRouten.Import.Upload, Formular(("rechnung.pdf", Pdf())));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonym_ist_401()
    {
        var client = _f.ErzeugeAnonymenClient();

        (await client.PostAsync(ApiRouten.Import.Upload, Formular(("rechnung.pdf", Pdf())))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync(ApiRouten.Import.Vorgaenge)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync(ApiRouten.Import.Vorgang.Replace("{id}", Guid.NewGuid().ToString()))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Pfadanteile_im_Dateinamen_werden_entfernt()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostAsync(
            ApiRouten.Import.Upload,
            Formular((@"..\..\x.pdf", Pdf()), ("/etc/y.pdf", Pdf()), ("a\u0007b.pdf", Pdf())));

        var vorgaenge = await Lese<ImportVorgangDto[]>(antwort);
        vorgaenge.Select(v => v.Dateiname).ShouldBe(["x.pdf", "y.pdf", "ab.pdf"]);
    }

    [Fact]
    public async Task Sehr_langer_Dateiname_wird_gekuerzt_und_behaelt_die_Endung()
    {
        var client = _f.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostAsync(ApiRouten.Import.Upload, Formular((new string('a', 300) + ".pdf", Pdf())));

        var vorgang = (await Lese<ImportVorgangDto[]>(antwort)).ShouldHaveSingleItem();
        vorgang.Dateiname.Length.ShouldBe(200);
        vorgang.Dateiname.ShouldEndWith(".pdf");
    }

    [Fact]
    public async Task Liste_filtert_nach_Quelle()
    {
        await _f.ImportiereAsync("a.txt", Pdf(), ImportQuelle.Upload);
        await _f.ImportiereAsync("b.txt", Pdf(), ImportQuelle.Ordner);
        await _f.ImportiereAsync("c.txt", Pdf(), ImportQuelle.Ordner);
        var client = _f.ErzeugeClient(Rolle.NurLesen);

        var alle = await Liste(client, "");
        var ordner = await Liste(client, "?quelle=Ordner");

        alle.Gesamt.ShouldBe(3);
        ordner.Gesamt.ShouldBe(2);
        ordner.Eintraege.ShouldAllBe(v => v.Quelle == ImportQuelle.Ordner);
    }

    [Fact]
    public async Task Liste_filtert_nach_Zeitraum_und_zeigt_die_letzten_7_Tage_als_Standard()
    {
        await _f.ImportiereAsync("alt.txt", Pdf());
        _f.Zeit.Vorspulen(TimeSpan.FromDays(10));
        await _f.ImportiereAsync("neu.txt", Pdf());
        var client = _f.ErzeugeClient(Rolle.NurLesen);

        (await Liste(client, "")).Eintraege.Select(v => v.Dateiname).ShouldBe(["neu.txt"]);
        (await Liste(client, "?tage=30")).Eintraege.Select(v => v.Dateiname).ShouldBe(["neu.txt", "alt.txt"]);
    }

    [Fact]
    public async Task Liste_blaettert_zu_50_neueste_zuerst()
    {
        for (var i = 1; i <= 55; i++)
        {
            await _f.ImportiereAsync($"datei{i:D2}.txt", Pdf());
            _f.Zeit.Vorspulen(TimeSpan.FromMinutes(1));
        }

        var client = _f.ErzeugeClient(Rolle.NurLesen);

        var erste = await Liste(client, "");
        var zweite = await Liste(client, "?seite=2");

        erste.Gesamt.ShouldBe(55);
        erste.Seite.ShouldBe(1);
        erste.Seitengroesse.ShouldBe(50);
        erste.Eintraege.Count.ShouldBe(50);
        erste.Eintraege[0].Dateiname.ShouldBe("datei55.txt");
        zweite.Seite.ShouldBe(2);
        zweite.Eintraege.Select(v => v.Dateiname).ShouldBe(["datei05.txt", "datei04.txt", "datei03.txt", "datei02.txt", "datei01.txt"]);
    }

    [Theory]
    [InlineData("?tage=0")]
    [InlineData("?tage=91")]
    [InlineData("?seite=0")]
    [InlineData("?quelle=Brieftaube")]
    public async Task Ungueltige_Abfrageparameter_liefern_400(string abfrage)
    {
        var client = _f.ErzeugeClient(Rolle.NurLesen);

        (await client.GetAsync(ApiRouten.Import.Vorgaenge + abfrage)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Einzelner_Vorgang_wird_geliefert_oder_404()
    {
        var vorgang = await _f.ImportiereAsync("rechnung.pdf", Pdf());
        var client = _f.ErzeugeClient(Rolle.NurLesen);

        var gefunden = await client.GetAsync(ApiRouten.Import.Vorgang.Replace("{id}", vorgang.Id.ToString()));
        var fehlt = await client.GetAsync(ApiRouten.Import.Vorgang.Replace("{id}", Guid.NewGuid().ToString()));

        gefunden.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Lese<ImportVorgangDto>(gefunden)).Id.ShouldBe(vorgang.Id);
        fehlt.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Sofort-Verarbeitung ohne die echte Formaterkennung: Sie würde die Platzhalter-PDFs ablehnen.</summary>
    private static ApiFabrik OhneFormaterkennung(ApiFabrik fabrik) =>
        fabrik.MitSofortVerarbeitung().MitDiensten(d =>
        {
            foreach (var eintrag in d.Where(e => e.ServiceType == typeof(IPipelineSchritt) && e.ImplementationType == typeof(FormaterkennungSchritt)).ToList())
            {
                d.Remove(eintrag);
            }
        });

    private static byte[] Pdf() => Encoding.UTF8.GetBytes($"%PDF-1.7 {Guid.NewGuid():N}");

    private static MultipartFormDataContent Formular(params (string Name, byte[] Inhalt)[] dateien)
    {
        var formular = new MultipartFormDataContent();
        foreach (var (name, inhalt) in dateien)
        {
            formular.Add(new ByteArrayContent(inhalt), "dateien", name);
        }

        return formular;
    }

    private static async Task<T> Lese<T>(HttpResponseMessage antwort)
        where T : class =>
        (await antwort.Content.ReadFromJsonAsync<T>(Json)).ShouldNotBeNull();

    private static async Task<SeiteDto<ImportVorgangDto>> Liste(HttpClient client, string abfrage) =>
        await Lese<SeiteDto<ImportVorgangDto>>(await client.GetAsync(ApiRouten.Import.Vorgaenge + abfrage));

    private async Task<int> Anzahl()
    {
        using var scope = _f.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().ImportVorgaenge.CountAsync();
    }
}
