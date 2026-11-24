using System.Net;
using System.Text.Json;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Einstellungen;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Einstellungen;

[Collection("Api")]
public sealed class EinstellungenTests(ApiFabrik fabrik) : IAsyncLifetime
{

    public async Task InitializeAsync()
    {
        await fabrik.ZuruecksetzenAsync();
        ((MemoryCache)fabrik.Services.GetRequiredService<IMemoryCache>()).Compact(1.0);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ohne_Datenbankeintrag_gelten_die_Standardwerte()
    {
        var dto = await LesenAsync(fabrik.ErzeugeClient(Rolle.NurLesen));

        dto.Ust.Status.ShouldBe(UstStatus.Unbekannt);
        dto.Pruefungen.SummenToleranz.ShouldBe(0.02m);
        dto.Pruefungen.KonfidenzSchwelle.ShouldBe(0.8m);
        dto.Di.Monatslimit.ShouldBe(500);
        dto.Aufbewahrung.Jahre.ShouldBe(8);
        dto.Pruefungen.Aktiv.Count.ShouldBe(9);
        dto.Pruefungen.Aktiv.Values.ShouldAllBe(aktiv => aktiv);
        dto.Allgemein.InboxTagId.ShouldBe(SystemIds.TagInbox); // Seed-Tag „Inbox“
    }

    public static TheoryData<string, object> Gruppen() => new()
    {
        { "allgemein", new AllgemeinEinstellungen { Firmenname = "Biber GmbH", Zeitzone = "Europe/Vienna", InboxTagId = SystemIds.TagInbox } },
        { "ust", new UstEinstellungen { Status = UstStatus.Regelbesteuerung, Zeitraum = UstvaZeitraum.Monatlich } },
        { "pruefungen", new PruefEinstellungen { SummenToleranz = 0.05m, MaxAlterJahre = 5, Aktiv = new() { [PruefungArt.Dublette] = false } } },
        { "di", new DiEinstellungen { Monatslimit = 1000, WarnungAbProzent = 90, MaxSeitenJeBeleg = 20 } },
        { "mail", new MailEinstellungen { Aktiv = true, AbrufintervallMinuten = 15, Eingangsordner = "Rechnungen" } },
        { "ordner", new OrdnerEinstellungen { PollingSekunden = 60, StabilSekunden = 5 } },
        { "aufbewahrung", new AufbewahrungEinstellungen { Jahre = 10 } },
        { "zugriff", new ZugriffEinstellungen { OffeneRegistrierung = true, GoogleAnmeldung = true } },
    };

    [Theory]
    [MemberData(nameof(Gruppen))]
    public async Task Speichern_und_erneut_lesen(string gruppe, object wert)
    {
        var client = fabrik.ErzeugeClient(Rolle.Admin);

        var antwort = await PutRohAsync(client, gruppe, JsonSerializer.Serialize(wert, wert.GetType(), JsonTest.Optionen));

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var dto = await LesenAsync(client);
        object gelesen = gruppe switch
        {
            "allgemein" => dto.Allgemein,
            "ust" => dto.Ust,
            "pruefungen" => dto.Pruefungen,
            "di" => dto.Di,
            "mail" => dto.Mail,
            "ordner" => dto.Ordner,
            "aufbewahrung" => dto.Aufbewahrung,
            _ => dto.Zugriff,
        };
        JsonSerializer.Serialize(gelesen, gelesen.GetType(), JsonTest.Optionen).ShouldBe(JsonSerializer.Serialize(wert, wert.GetType(), JsonTest.Optionen));
    }

    [Fact]
    public async Task Speichern_verwirft_den_Zwischenspeicher()
    {
        using var scope = fabrik.ErzeugeScope();
        var einstellungen = scope.ServiceProvider.GetRequiredService<IEinstellungen>();
        (await einstellungen.LesenAsync<AufbewahrungEinstellungen>(CancellationToken.None)).Jahre.ShouldBe(8);

        await einstellungen.SpeichernAsync(new AufbewahrungEinstellungen { Jahre = 12 }, CancellationToken.None);

        (await einstellungen.LesenAsync<AufbewahrungEinstellungen>(CancellationToken.None)).Jahre.ShouldBe(12);
        (await LesenAsync(fabrik.ErzeugeClient())).Aufbewahrung.Jahre.ShouldBe(12);
    }

    [Fact]
    public async Task Schluessel_ist_Typname_ohne_Endung()
    {
        using var scope = fabrik.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<IEinstellungen>()
            .SpeichernAsync(new PruefEinstellungen { MaxAlterJahre = 3 }, CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Einstellungen.Select(e => e.Schluessel).ToListAsync()).ShouldBe(["pruef"]);
    }

    [Theory]
    [InlineData("pruefungen", """{"summenToleranz":11}""", "summenToleranz")]
    [InlineData("pruefungen", """{"maxAlterJahre":0}""", "maxAlterJahre")]
    [InlineData("pruefungen", """{"kleinbetragGrenze":1001}""", "kleinbetragGrenze")]
    [InlineData("pruefungen", """{"konfidenzSchwelle":1.5}""", "konfidenzSchwelle")]
    [InlineData("di", """{"monatslimit":-1}""", "monatslimit")]
    [InlineData("di", """{"warnungAbProzent":0}""", "warnungAbProzent")]
    [InlineData("di", """{"maxSeitenJeBeleg":501}""", "maxSeitenJeBeleg")]
    [InlineData("mail", """{"abrufintervallMinuten":1441}""", "abrufintervallMinuten")]
    [InlineData("mail", """{"eingangsordner":" "}""", "eingangsordner")]
    [InlineData("mail", """{"verarbeitetOrdner":""}""", "verarbeitetOrdner")]
    [InlineData("mail", """{"ohneBelegOrdner":""}""", "ohneBelegOrdner")]
    [InlineData("ordner", """{"pollingSekunden":4}""", "pollingSekunden")]
    [InlineData("ordner", """{"stabilSekunden":601}""", "stabilSekunden")]
    [InlineData("aufbewahrung", """{"jahre":31}""", "jahre")]
    public async Task Ungueltige_Werte_liefern_400_mit_Feldnamen(string gruppe, string inhalt, string feld)
    {
        var antwort = await PutRohAsync(fabrik.ErzeugeClient(), gruppe, inhalt);

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FehlerAsync(antwort)).ShouldContainKey(feld);
    }

    [Fact]
    public async Task Zu_langer_Firmenname_liefert_400()
    {
        var antwort = await fabrik.ErzeugeClient().PutJsonAsync(
            "/api/einstellungen/allgemein", new AllgemeinEinstellungen { Firmenname = new string('x', 201) });

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FehlerAsync(antwort)).ShouldContainKey("firmenname");
    }

    [Fact]
    public async Task Unbekannte_Gruppe_liefert_404()
    {
        var antwort = await PutRohAsync(fabrik.ErzeugeClient(), "unbekannt", "{}");

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Rolle.Benutzer)]
    [InlineData(Rolle.NurLesen)]
    public async Task Ohne_Admin_Rolle_nur_lesen(Rolle rolle)
    {
        var client = fabrik.ErzeugeClient(rolle);

        (await client.GetAsync("/api/einstellungen")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutRohAsync(client, "aufbewahrung", """{"jahre":10}""")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Umgebung_enthaelt_keine_Schluessel_und_Passwoerter()
    {
        const string schluessel = "GEHEIMER-DI-SCHLUESSEL-4711";
        const string passwort = "GEHEIMES-IMAP-PASSWORT-0815";
        const string googleGeheimnis = "GEHEIMES-GOOGLE-SECRET-42";
        await using var mitGeheimnissen = fabrik.WithWebHostBuilder(b =>
        {
            b.UseSetting("DocumentIntelligence:Endpunkt", "https://di.example.test/");
            b.UseSetting("DocumentIntelligence:Schluessel", schluessel);
            b.UseSetting("Imap:Host", "imap.example.test");
            b.UseSetting("Imap:Port", "993");
            b.UseSetting("Imap:Benutzer", "rechnungen@example.test");
            b.UseSetting("Imap:Passwort", passwort);
            b.UseSetting("Google:ClientId", "client-id");
            b.UseSetting("Google:ClientSecret", googleGeheimnis);
            b.UseSetting("Anwendung:OeffentlicheUrl", "http://rechnungen.lan");
        });
        var client = mitGeheimnissen.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolleHeader, nameof(Rolle.Admin));

        var text = await client.GetStringAsync("/api/einstellungen");

        text.ShouldNotContain(schluessel);
        text.ShouldNotContain(passwort);
        text.ShouldNotContain(googleGeheimnis);
        var umgebung = JsonSerializer.Deserialize<EinstellungenDto>(text, JsonTest.Optionen)!.Umgebung;
        umgebung.DiEndpunkt.ShouldBe("https://di.example.test/");
        umgebung.DiModell.ShouldBe("prebuilt-invoice");
        umgebung.ImapServer.ShouldBe("imap.example.test:993");
        umgebung.ImapBenutzer.ShouldBe("rechnungen@example.test");
        umgebung.OeffentlicheUrl.ShouldBe("http://rechnungen.lan");
        umgebung.DiKonfiguriert.ShouldBeTrue();
        umgebung.ImapKonfiguriert.ShouldBeTrue();
        umgebung.GoogleKonfiguriert.ShouldBeTrue();
        umgebung.AblagePfad.ShouldBe(Path.Combine(fabrik.TempOrdner, "ablage"));
    }

    [Fact]
    public async Task Inbox_Tag_Wechsel_setzt_das_Kennzeichen_exklusiv()
    {
        var alt = Testdaten.Tag();
        alt.IstInbox = true;
        await fabrik.SpeichernAsync(alt);
        var neu = await fabrik.SpeichernAsync(Testdaten.Tag());
        var client = fabrik.ErzeugeClient();

        var antwort = await client.PutJsonAsync("/api/einstellungen/allgemein", new AllgemeinEinstellungen { InboxTagId = neu.Id });

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Tags.Where(t => t.IstInbox).Select(t => t.Id).ToListAsync()).ShouldBe([neu.Id]);
        (await LesenAsync(client)).Allgemein.InboxTagId.ShouldBe(neu.Id);
    }

    [Fact]
    public async Task System_Tag_ist_als_Inbox_nicht_waehlbar()
    {
        var system = Testdaten.Tag();
        system.IstSystem = true;
        await fabrik.SpeichernAsync(system);

        var antwort = await fabrik.ErzeugeClient().PutJsonAsync(
            "/api/einstellungen/allgemein", new AllgemeinEinstellungen { InboxTagId = system.Id });

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FehlerAsync(antwort)).ShouldContainKey("inboxTagId");
    }

    [Fact]
    public async Task Aenderung_erzeugt_Audit_Eintrag()
    {
        var antwort = await PutRohAsync(fabrik.ErzeugeClient(), "aufbewahrung", """{"jahre":10}""");

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var eintrag = await db.AuditEintraege.SingleAsync(a => a.Entitaet == "Einstellung");
        eintrag.EntitaetId.ShouldBe("aufbewahrung");
        eintrag.Feld.ShouldBe("jahre");
        eintrag.AlterWert.ShouldBe("8");
        eintrag.NeuerWert.ShouldBe("10");
        eintrag.BenutzerId.ShouldBe(ApiFabrik.BenutzerId(Rolle.Admin));
    }

    private static async Task<EinstellungenDto> LesenAsync(HttpClient client)
    {
        var antwort = await client.GetAsync("/api/einstellungen");
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await antwort.Content.LesenAsync<EinstellungenDto>())!;
    }

    private static Task<HttpResponseMessage> PutRohAsync(HttpClient client, string gruppe, string inhalt) =>
        client.PutAsync($"/api/einstellungen/{gruppe}", new StringContent(inhalt, System.Text.Encoding.UTF8, "application/json"));

    private static async Task<Dictionary<string, string[]>> FehlerAsync(HttpResponseMessage antwort)
    {
        using var dokument = JsonDocument.Parse(await antwort.Content.ReadAsStringAsync());
        return dokument.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }
}
