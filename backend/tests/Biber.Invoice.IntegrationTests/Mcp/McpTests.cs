using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ArtikelEntitaet = Biber.Invoice.Domain.Entitaeten.Artikel;

namespace Biber.Invoice.IntegrationTests.Mcp;

[Collection("Api")]
public sealed class McpTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private readonly MerkPlaner _planer = new();
    private ApiFabrik? _mcpFabrik;

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
        foreach (var rolle in Enum.GetValues<Rolle>())
        {
            await LegeBenutzerAnAsync(rolle);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Echte Anmeldung; die Pipeline wird nur eingeplant, nicht ausgeführt.</summary>
    private ApiFabrik McpFabrik => _mcpFabrik ??= fabrik
        .MitDiensten(d =>
        {
            d.AddSingleton<TimeProvider>(fabrik.Zeit);
            d.RemoveAll<IVerarbeitungsPlaner>();
            d.AddSingleton<IVerarbeitungsPlaner>(_planer);
        })
        .MitEchterAnmeldung();

    [Fact]
    public async Task Tools_list_liefert_genau_die_sieben_Tools()
    {
        await using var client = await ClientAsync(await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen));

        var tools = await client.ListToolsAsync();

        tools.Select(t => t.Name).ShouldBe(
            ["belege_suchen", "beleg_abrufen", "kosten_auswerten", "korrespondent_abrufen", "artikel_suchen", "artikel_preisverlauf", "beleg_hochladen"],
            ignoreOrder: true);
        tools.ShouldAllBe(t => !string.IsNullOrWhiteSpace(t.Description));
        client.ServerInfo.ShouldNotBeNull().Name.ShouldBe("biber-rechnungen");
    }

    [Fact]
    public async Task Belege_suchen_findet_Toner_und_filtert_nach_Korrespondent_und_Kategorie()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Druckerwelt GmbH"));
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie("Büromaterial"));
        var toner = await fabrik.SpeichernAsync(Testdaten.Beleg(
            rechnungsnummer: "RE-4711", brutto: 59.50m, korrespondentId: korrespondent.Id, kategorieId: kategorie.Id,
            volltext: "Toner schwarz für Laserdrucker"));
        await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Kugelschreiber"));
        await using var client = await ClientAsync(await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen));

        var ergebnis = await AufrufAsync(client, "belege_suchen", new() { ["suche"] = "Toner" });
        var gefiltert = await AufrufAsync(client, "belege_suchen", new() { ["korrespondent"] = "druckerwelt gmbh", ["kategorie"] = "Büromaterial" });

        var beleg = ergebnis.GetProperty("belege").EnumerateArray().ShouldHaveSingleItem();
        beleg.GetProperty("id").GetGuid().ShouldBe(toner.Id);
        beleg.GetProperty("rechnungsnummer").GetString().ShouldBe("RE-4711");
        beleg.GetProperty("korrespondent").GetString().ShouldBe("Druckerwelt GmbH");
        beleg.GetProperty("kategorie").GetString().ShouldBe("Büromaterial");
        beleg.GetProperty("brutto").GetDecimal().ShouldBe(59.50m);
        beleg.GetProperty("datum").GetString().ShouldBe("2026-01-10");
        beleg.GetProperty("link").GetString().ShouldEndWith($"/belege/{toner.Id}");
        gefiltert.GetProperty("belege").EnumerateArray().ShouldHaveSingleItem().GetProperty("id").GetGuid().ShouldBe(toner.Id);
    }

    [Fact]
    public async Task Unbekannter_Korrespondent_ergibt_Fehler_mit_Vorschlaegen()
    {
        await fabrik.SpeichernAsync(Testdaten.Korrespondent("Druckerwelt GmbH"));
        await fabrik.SpeichernAsync(Testdaten.Korrespondent("Stadtwerke"));
        await using var client = await ClientAsync(await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen));

        string fehler = await FehlerAsync(client, "belege_suchen", new() { ["korrespondent"] = "Druckerwlt" });
        string fehlerAbruf = await FehlerAsync(client, "korrespondent_abrufen", new() { ["name_oder_id"] = "Druckerwlt" });

        fehler.ShouldContain("nicht gefunden");
        fehler.ShouldContain("Druckerwelt GmbH");
        fehlerAbruf.ShouldContain("Druckerwelt GmbH");
    }

    [Fact]
    public async Task Beleg_abrufen_liefert_Positionen_Steuerzeilen_und_Link()
    {
        var beleg = Testdaten.Beleg(rechnungsnummer: "RE-1", brutto: 119m);
        beleg.Positionen.Add(new Position { Id = Guid.NewGuid(), Nr = 1, Text = "Toner schwarz", Menge = 2m, Einzelpreis = 50m, Gesamt = 100m });
        beleg.Steuerzeilen.Add(new Steuerzeile { Id = Guid.NewGuid(), Satz = 19m, Netto = 100m, Steuer = 19m });
        await fabrik.SpeichernAsync(beleg);
        await using var client = await ClientAsync(await TokenAsync(Rolle.NurLesen, TokenScopes.Lesen));

        var detail = await AufrufAsync(client, "beleg_abrufen", new() { ["id"] = beleg.Id.ToString() });

        detail.GetProperty("rechnungsnummer").GetString().ShouldBe("RE-1");
        var position = detail.GetProperty("positionen").EnumerateArray().ShouldHaveSingleItem();
        position.GetProperty("text").GetString().ShouldBe("Toner schwarz");
        position.GetProperty("gesamt").GetDecimal().ShouldBe(100m);
        detail.GetProperty("steuerzeilen").EnumerateArray().ShouldHaveSingleItem().GetProperty("satz").GetDecimal().ShouldBe(19m);
        detail.GetProperty("link").GetString().ShouldEndWith($"/belege/{beleg.Id}");
        (await FehlerAsync(client, "beleg_abrufen", new() { ["id"] = Guid.NewGuid().ToString() })).ShouldContain("nicht gefunden");
    }

    [Fact]
    public async Task Kosten_auswerten_gruppiert_nach_Kategorie()
    {
        var buero = await fabrik.SpeichernAsync(Testdaten.Kategorie("Büro"));
        var energie = await fabrik.SpeichernAsync(Testdaten.Kategorie("Energie"));
        await fabrik.SpeichernAsync(Testdaten.Beleg(brutto: 100m, kategorieId: buero.Id, rechnungsdatum: new DateOnly(2026, 2, 1)));
        await fabrik.SpeichernAsync(Testdaten.Beleg(brutto: 50m, kategorieId: buero.Id, rechnungsdatum: new DateOnly(2026, 2, 3)));
        await fabrik.SpeichernAsync(Testdaten.Beleg(brutto: 200m, kategorieId: energie.Id, rechnungsdatum: new DateOnly(2026, 2, 5)));
        await using var client = await ClientAsync(await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen));

        var kosten = await AufrufAsync(client, "kosten_auswerten", new() { ["von"] = "2026-01-01", ["bis"] = "2026-12-31", ["gruppierung"] = "kategorie" });

        kosten.GetProperty("summeBrutto").GetDecimal().ShouldBe(350m);
        var zeilen = kosten.GetProperty("zeilen").EnumerateArray()
            .ToDictionary(z => z.GetProperty("bezeichnung").GetString()!, z => z.GetProperty("brutto").GetDecimal());
        zeilen["Büro"].ShouldBe(150m);
        zeilen["Energie"].ShouldBe(200m);
        (await FehlerAsync(client, "kosten_auswerten", new() { ["von"] = "2026-01-01", ["bis"] = "2026-12-31", ["gruppierung"] = "woche" }))
            .ShouldContain("Gruppierung");
    }

    [Fact]
    public async Task Korrespondent_abrufen_nach_Name_mit_Kosten_und_letzten_Belegen()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Stadtwerke Musterstadt"));
        for (int i = 1; i <= 6; i++)
        {
            await fabrik.SpeichernAsync(Testdaten.Beleg(brutto: 10m, korrespondentId: k.Id, rechnungsdatum: new DateOnly(2026, 1, i)));
        }

        await using var client = await ClientAsync(await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen));

        var ergebnis = await AufrufAsync(client, "korrespondent_abrufen", new() { ["name_oder_id"] = "Stadtwerke Musterstadt" });

        ergebnis.GetProperty("id").GetGuid().ShouldBe(k.Id);
        ergebnis.GetProperty("kostenJahr").GetDecimal().ShouldBe(60m);
        ergebnis.GetProperty("anzahlBelege").GetInt32().ShouldBe(6);
        ergebnis.GetProperty("letzteBelege").GetArrayLength().ShouldBe(5);
    }

    [Fact]
    public async Task Artikel_suchen_und_Preisverlauf()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Papierhaus"));
        var artikel = await fabrik.SpeichernAsync(new ArtikelEntitaet
        {
            Id = Guid.NewGuid(),
            KorrespondentId = k.Id,
            Name = "Kopierpapier A4",
            NameNormalisiert = "kopierpapier a4",
            Einheit = "Pack",
        });
        await fabrik.SpeichernAsync(BelegMitArtikel(k, artikel, new DateOnly(2025, 3, 1), 4.99m));
        await fabrik.SpeichernAsync(BelegMitArtikel(k, artikel, new DateOnly(2025, 9, 1), 5.49m));
        await using var client = await ClientAsync(await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen));

        var suche = await AufrufAsync(client, "artikel_suchen", new() { ["suche"] = "Kopierpapier" });
        var verlauf = await AufrufAsync(client, "artikel_preisverlauf", new() { ["id"] = artikel.Id.ToString(), ["jahr"] = 2025 });

        var treffer = suche.GetProperty("artikel").EnumerateArray().ShouldHaveSingleItem();
        treffer.GetProperty("id").GetGuid().ShouldBe(artikel.Id);
        treffer.GetProperty("korrespondent").GetString().ShouldBe("Papierhaus");
        treffer.GetProperty("letzterPreis").GetDecimal().ShouldBe(5.49m);
        var serie = verlauf.GetProperty("serien").EnumerateArray().ShouldHaveSingleItem();
        serie.GetProperty("korrespondent").GetString().ShouldBe("Papierhaus");
        serie.GetProperty("punkte").EnumerateArray().Select(p => p.GetProperty("einzelpreis").GetDecimal()).ShouldBe([4.99m, 5.49m]);
    }

    [Fact]
    public async Task Beleg_hochladen_legt_Vorgang_mit_Quelle_Mcp_und_Token_an()
    {
        var token = await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen, TokenScopes.Upload);
        await using var client = await ClientAsync(token);
        string pdf = Convert.ToBase64String(Encoding.UTF8.GetBytes($"%PDF-1.7 {Guid.NewGuid():N}"));

        var ergebnis = await AufrufAsync(client, "beleg_hochladen", new() { ["dateiname"] = "rechnung.pdf", ["inhalt_base64"] = pdf });

        var vorgangId = ergebnis.GetProperty("vorgangId").GetGuid();
        ergebnis.GetProperty("status").GetString().ShouldNotBeNullOrEmpty();
        var vorgang = await fabrik.VorgangAsync(vorgangId);
        vorgang.Quelle.ShouldBe(ImportQuelle.Mcp);
        vorgang.ApiTokenId.ShouldBe(token.Id);
        vorgang.BenutzerId.ShouldBe(ApiFabrik.BenutzerId(Rolle.Benutzer));
        vorgang.Dateiname.ShouldBe("rechnung.pdf");
        _planer.Eingeplant.ShouldContain(vorgangId);
    }

    [Fact]
    public async Task Beleg_hochladen_ohne_Scope_upload_oder_mit_NurLesen_ist_ein_Tool_Fehler()
    {
        string pdf = Convert.ToBase64String(Encoding.UTF8.GetBytes("%PDF-1.7 x"));
        var argumente = new Dictionary<string, object?> { ["dateiname"] = "rechnung.pdf", ["inhalt_base64"] = pdf };
        await using var ohneScope = await ClientAsync(await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen));

        (await FehlerAsync(ohneScope, "beleg_hochladen", argumente)).ShouldContain("Scope upload fehlt");

        // NurLesen kann keinen Upload-Token anlegen; ein solcher Token (z. B. nach Rollenwechsel) bleibt trotzdem ohne Upload.
        var token = await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen, TokenScopes.Upload);
        await SetzeRolleAsync(Rolle.Benutzer, Rolle.NurLesen);
        await using var nurLesen = await ClientAsync(token);
        (await FehlerAsync(nurLesen, "beleg_hochladen", argumente)).ShouldContain("Nur lesen");

        using var scope = fabrik.ErzeugeScope();
        (await scope.ServiceProvider.GetRequiredService<BiberDbContext>().ImportVorgaenge.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Ungueltiges_Base64_ist_ein_Tool_Fehler()
    {
        await using var client = await ClientAsync(await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen, TokenScopes.Upload));

        string fehler = await FehlerAsync(client, "beleg_hochladen", new() { ["dateiname"] = "rechnung.pdf", ["inhalt_base64"] = "kein base64!" });

        fehler.ShouldContain("Base64");
    }

    [Fact]
    public async Task Ohne_Token_und_mit_widerrufenem_Token_401()
    {
        var token = await TokenAsync(Rolle.Benutzer, TokenScopes.Lesen);
        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer).PostAsync(ApiRouten.Tokens.Widerrufen.Replace("{id}", token.Id.ToString()), null);
        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RohAufrufAsync(null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RohAufrufAsync(token.Klartext)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RohAufrufAsync("brk_" + new string('x', 40))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpResponseMessage> RohAufrufAsync(string? klartext)
    {
        var http = McpFabrik.CreateClient();
        if (klartext is not null)
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", klartext);
        }

        using var anfrage = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list" }),
        };
        anfrage.Headers.Accept.ParseAdd("application/json");
        anfrage.Headers.Accept.ParseAdd("text/event-stream");
        return await http.SendAsync(anfrage);
    }

    private async Task<McpClient> ClientAsync(Token token)
    {
        var http = McpFabrik.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Klartext);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
            http,
            NullLoggerFactory.Instance,
            ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    private static async Task<CallToolResult> RufeAsync(McpClient client, string tool, Dictionary<string, object?> argumente) =>
        await client.CallToolAsync(tool, argumente);

    private static async Task<JsonElement> AufrufAsync(McpClient client, string tool, Dictionary<string, object?> argumente)
    {
        var ergebnis = await RufeAsync(client, tool, argumente);
        string text = Text(ergebnis);
        (ergebnis.IsError ?? false).ShouldBeFalse(text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> FehlerAsync(McpClient client, string tool, Dictionary<string, object?> argumente)
    {
        var ergebnis = await RufeAsync(client, tool, argumente);
        string text = Text(ergebnis);
        (ergebnis.IsError ?? false).ShouldBeTrue(text);
        text.ShouldNotContain(" at ");
        return text;
    }

    private static string Text(CallToolResult ergebnis) =>
        string.Concat(ergebnis.Content.OfType<TextContentBlock>().Select(c => c.Text));

    private sealed record Token(Guid Id, string Klartext);

    private async Task<Token> TokenAsync(Rolle rolle, params string[] scopes)
    {
        var antwort = await fabrik.ErzeugeClient(rolle)
            .PostAsJsonAsync(ApiRouten.Tokens.Liste, new ApiTokenAnlegenDto($"Claude {Guid.NewGuid():N}"[..20], scopes));
        antwort.StatusCode.ShouldBe(HttpStatusCode.Created);
        var erstellt = (await antwort.Content.ReadFromJsonAsync<ApiTokenErstelltDto>()).ShouldNotBeNull();
        return new Token(erstellt.Token.Id, erstellt.Klartext);
    }

    private static Beleg BelegMitArtikel(Korrespondent k, ArtikelEntitaet artikel, DateOnly datum, decimal preis)
    {
        var beleg = Testdaten.Beleg(rechnungsdatum: datum, korrespondentId: k.Id);
        beleg.Positionen.Add(new Position
        {
            Id = Guid.NewGuid(),
            Nr = 1,
            Text = artikel.Name,
            Menge = 1m,
            Einheit = artikel.Einheit,
            Einzelpreis = preis,
            Gesamt = preis,
            ArtikelId = artikel.Id,
        });
        return beleg;
    }

    private async Task SetzeRolleAsync(Rolle benutzerVon, Rolle neueRolle)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var id = ApiFabrik.BenutzerId(benutzerVon);
        var rolleId = await db.Roles.Where(r => r.Name == neueRolle.ToString()).Select(r => r.Id).SingleAsync();
        db.UserRoles.RemoveRange(db.UserRoles.Where(r => r.UserId == id));
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = id, RoleId = rolleId });
        await db.SaveChangesAsync();
    }

    private async Task LegeBenutzerAnAsync(Rolle rolle)
    {
        var benutzer = Testdaten.Benutzer();
        benutzer.Id = ApiFabrik.BenutzerId(rolle);
        await fabrik.SpeichernAsync(benutzer);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var eintrag = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = rolle.ToString(),
            NormalizedName = rolle.ToString().ToUpperInvariant(),
        };
        db.Roles.Add(eintrag);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = benutzer.Id, RoleId = eintrag.Id });
        await db.SaveChangesAsync();
    }

    /// <summary>Merkt sich eingeplante Vorgänge, ohne die Pipeline auszuführen.</summary>
    private sealed class MerkPlaner : IVerarbeitungsPlaner
    {
        public List<Guid> Eingeplant { get; } = [];

        public void Einplanen(Guid importVorgangId, TimeSpan? verzoegerung = null) => Eingeplant.Add(importVorgangId);
    }
}
