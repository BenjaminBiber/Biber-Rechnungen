using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Benachrichtigungen;

[Collection("Api")]
public sealed class BenachrichtigungTests(ApiFabrik fabrik) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
        await LegeBenutzerAnAsync(Rolle.Admin);
        await LegeBenutzerAnAsync(Rolle.Benutzer);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Gemeldete_Benachrichtigung_erscheint_in_Liste_und_Zaehler()
    {
        await MeldenAsync(Meldung("Import fehlgeschlagen"));
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var liste = await HoleListeAsync(client);
        var zaehler = await client.GetJsonAsync<BenachrichtigungZaehlerDto>(ApiRouten.Benachrichtigungen.Zaehler);

        var eintrag = liste.Eintraege.ShouldHaveSingleItem();
        eintrag.Titel.ShouldBe("Import fehlgeschlagen");
        eintrag.Text.ShouldBe("Text");
        eintrag.Gelesen.ShouldBeFalse();
        liste.Gesamt.ShouldBe(1);
        zaehler.ShouldNotBeNull().Ungelesen.ShouldBe(1);
    }

    [Fact]
    public async Task Liste_ist_neueste_zuerst_und_in_Seiten_zu_20()
    {
        for (var i = 1; i <= 25; i++)
        {
            fabrik.Zeit.Vorspulen(TimeSpan.FromSeconds(1));
            await MeldenAsync(Meldung($"Meldung {i}"));
        }

        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var erste = await HoleListeAsync(client, seite: 1);
        var zweite = await HoleListeAsync(client, seite: 2);

        erste.Eintraege.Count.ShouldBe(20);
        erste.Gesamt.ShouldBe(25);
        erste.Seitengroesse.ShouldBe(20);
        erste.Eintraege[0].Titel.ShouldBe("Meldung 25");
        zweite.Eintraege.Select(e => e.Titel).ShouldBe(["Meldung 5", "Meldung 4", "Meldung 3", "Meldung 2", "Meldung 1"]);
    }

    [Fact]
    public async Task Gleicher_Schluessel_ergibt_einen_Eintrag()
    {
        await MeldenAsync(Meldung("Erste", schluessel: "di-limit"));
        await MeldenAsync(Meldung("Zweite", schluessel: "di-limit"));

        var liste = await HoleListeAsync(fabrik.ErzeugeClient(Rolle.Benutzer));

        liste.Eintraege.ShouldHaveSingleItem().Titel.ShouldBe("Erste");
    }

    [Fact]
    public async Task Gleicher_Schluessel_parallel_ergibt_einen_Eintrag()
    {
        var aufrufe = Enumerable.Range(0, 12).Select(_ => MeldenAsync(Meldung("Parallel", schluessel: "parallel")));

        await Task.WhenAll(aufrufe);

        var liste = await HoleListeAsync(fabrik.ErzeugeClient(Rolle.Benutzer));
        liste.Eintraege.ShouldHaveSingleItem().Titel.ShouldBe("Parallel");
    }

    [Fact]
    public async Task Ohne_Schluessel_entstehen_mehrere_Eintraege()
    {
        await MeldenAsync(Meldung("Gleich"));
        await MeldenAsync(Meldung("Gleich"));

        var liste = await HoleListeAsync(fabrik.ErzeugeClient(Rolle.Benutzer));

        liste.Eintraege.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Titel_und_Text_werden_gekuerzt()
    {
        await MeldenAsync(Meldung(new string('T', 500), text: new string('X', 2000)));

        var eintrag = (await HoleListeAsync(fabrik.ErzeugeClient(Rolle.Benutzer))).Eintraege.ShouldHaveSingleItem();

        eintrag.Titel.Length.ShouldBe(200);
        eintrag.Text.Length.ShouldBe(1000);
    }

    [Fact]
    public async Task Gelesen_gilt_je_Benutzer()
    {
        await MeldenAsync(Meldung("Für beide"));
        var id = (await HoleListeAsync(fabrik.ErzeugeClient(Rolle.Benutzer))).Eintraege.Single().Id;
        var benutzer = fabrik.ErzeugeClient(Rolle.Benutzer);
        var admin = fabrik.ErzeugeClient(Rolle.Admin);

        var antwort = await benutzer.PostAsync(Gelesen(id), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await HoleListeAsync(benutzer)).Eintraege.Single().Gelesen.ShouldBeTrue();
        (await Zaehler(benutzer)).ShouldBe(0);
        (await HoleListeAsync(benutzer, nurUngelesen: true)).Eintraege.ShouldBeEmpty();
        (await HoleListeAsync(admin)).Eintraege.Single().Gelesen.ShouldBeFalse();
        (await Zaehler(admin)).ShouldBe(1);
        (await HoleListeAsync(admin, nurUngelesen: true)).Eintraege.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Gelesen_ist_idempotent()
    {
        await MeldenAsync(Meldung("Einmal"));
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);
        var id = (await HoleListeAsync(client)).Eintraege.Single().Id;

        (await client.PostAsync(Gelesen(id), null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostAsync(Gelesen(id), null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Zaehler(client)).ShouldBe(0);
    }

    [Fact]
    public async Task Alle_gelesen_setzt_den_Zaehler_auf_0()
    {
        await MeldenAsync(Meldung("Eins"));
        await MeldenAsync(Meldung("Zwei"));
        await MeldenAsync(Meldung("Drei"));
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);
        var admin = fabrik.ErzeugeClient(Rolle.Admin);
        var erste = (await HoleListeAsync(client)).Eintraege[0].Id;
        await client.PostAsync(Gelesen(erste), null);
        (await Zaehler(client)).ShouldBe(2);

        var antwort = await client.PostAsync(ApiRouten.Benachrichtigungen.AlleGelesen, null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Zaehler(client)).ShouldBe(0);
        (await HoleListeAsync(client)).Eintraege.ShouldAllBe(e => e.Gelesen);
        (await Zaehler(admin)).ShouldBe(3);
    }

    [Fact]
    public async Task NurAdmins_ist_fuer_Benutzer_unsichtbar()
    {
        await MeldenAsync(Meldung("Nur Admin", nurAdmins: true));
        await MeldenAsync(Meldung("Für alle"));
        var benutzer = fabrik.ErzeugeClient(Rolle.Benutzer);
        var admin = fabrik.ErzeugeClient(Rolle.Admin);
        var adminEintrag = (await HoleListeAsync(admin)).Eintraege.Single(e => e.Titel == "Nur Admin");

        (await HoleListeAsync(benutzer)).Eintraege.Select(e => e.Titel).ShouldBe(["Für alle"]);
        (await Zaehler(benutzer)).ShouldBe(1);
        (await HoleListeAsync(admin)).Eintraege.Count.ShouldBe(2);
        (await Zaehler(admin)).ShouldBe(2);
        (await benutzer.PostAsync(Gelesen(adminEintrag.Id), null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await benutzer.PostAsync(ApiRouten.Benachrichtigungen.AlleGelesen, null);
        (await HoleGelesenZahlAsync(Rolle.Benutzer)).ShouldBe(1);
    }

    [Fact]
    public async Task Meldungen_vor_dem_Anlegen_des_Benutzers_zaehlen_nicht()
    {
        await MeldenAsync(Meldung("Alt"));
        var spaeter = await LegeBenutzerAnAsync(Rolle.NurLesen, erstelltAm: fabrik.Zeit.GetUtcNow().AddHours(1));
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);
        spaeter.ShouldNotBeNull();

        (await HoleListeAsync(client)).Eintraege.ShouldBeEmpty();
        (await Zaehler(client)).ShouldBe(0);

        fabrik.Zeit.Vorspulen(TimeSpan.FromHours(2));
        await MeldenAsync(Meldung("Neu"));

        (await HoleListeAsync(client)).Eintraege.ShouldHaveSingleItem().Titel.ShouldBe("Neu");
        (await Zaehler(client)).ShouldBe(1);
    }

    [Fact]
    public async Task Unbekannte_Id_liefert_404()
    {
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostAsync(Gelesen(Guid.NewGuid()), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Seite_unter_1_liefert_400()
    {
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.GetAsync($"{ApiRouten.Benachrichtigungen.Liste}?seite=0");

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("GET", "/api/benachrichtigungen")]
    [InlineData("GET", "/api/benachrichtigungen/zaehler")]
    [InlineData("POST", "/api/benachrichtigungen/00000000-0000-0000-0000-000000000001/gelesen")]
    [InlineData("POST", "/api/benachrichtigungen/alle-gelesen")]
    public async Task Anonym_liefert_401(string methode, string pfad)
    {
        var client = fabrik.ErzeugeAnonymenClient();

        var antwort = await client.SendAsync(new HttpRequestMessage(new HttpMethod(methode), pfad));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NurLesen_darf_lesen_und_als_gelesen_markieren()
    {
        await LegeBenutzerAnAsync(Rolle.NurLesen);
        await MeldenAsync(Meldung("Für Leser"));
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await Zaehler(client)).ShouldBe(1);
        (await client.PostAsync(ApiRouten.Benachrichtigungen.AlleGelesen, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Zaehler(client)).ShouldBe(0);
    }

    private static string Gelesen(Guid id) => ApiRouten.Benachrichtigungen.Gelesen.Replace("{id}", id.ToString());

    private static NeueBenachrichtigung Meldung(
        string titel, string text = "Text", string? schluessel = null, bool nurAdmins = false) =>
        new(BenachrichtigungArt.ImportFehler, Schweregrad.Warnung, titel, text, NurAdmins: nurAdmins, EntdoppelungsSchluessel: schluessel);

    private async Task MeldenAsync(NeueBenachrichtigung meldung)
    {
        using var scope = fabrik.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<IBenachrichtigungsDienst>().MeldenAsync(meldung, CancellationToken.None);
    }

    private async Task<Benutzer> LegeBenutzerAnAsync(Rolle rolle, DateTimeOffset? erstelltAm = null)
    {
        var benutzer = Testdaten.Benutzer();
        benutzer.Id = ApiFabrik.BenutzerId(rolle);
        benutzer.ErstelltAm = erstelltAm ?? fabrik.Zeit.GetUtcNow();
        return await fabrik.SpeichernAsync(benutzer);
    }

    private static async Task<SeiteDto<BenachrichtigungDto>> HoleListeAsync(
        HttpClient client, bool nurUngelesen = false, int seite = 1)
    {
        var liste = await client.GetJsonAsync<SeiteDto<BenachrichtigungDto>>(
            $"{ApiRouten.Benachrichtigungen.Liste}?nurUngelesen={nurUngelesen}&seite={seite}");
        return liste.ShouldNotBeNull();
    }

    private static async Task<int> Zaehler(HttpClient client) =>
        (await client.GetJsonAsync<BenachrichtigungZaehlerDto>(ApiRouten.Benachrichtigungen.Zaehler))
        .ShouldNotBeNull().Ungelesen;

    private async Task<int> HoleGelesenZahlAsync(Rolle rolle)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var id = ApiFabrik.BenutzerId(rolle);
        return await db.BenachrichtigungenGelesen.CountAsync(g => g.BenutzerId == id);
    }
}
