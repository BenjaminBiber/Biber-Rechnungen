using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Tokens;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Tokens;

[Collection("Api")]
public sealed class TokenTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private const string McpLesenPfad = "/mcp/test";
    private const string McpUploadPfad = "/mcp/test-upload";

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
        foreach (var rolle in Enum.GetValues<Rolle>())
        {
            await LegeBenutzerAnAsync(rolle);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Erzeugter_Token_hat_Praefix_und_Laenge_und_nur_der_Hash_wird_gespeichert()
    {
        var erstellt = await ErzeugeAsync(fabrik.ErzeugeClient(Rolle.Benutzer), "Claude", TokenScopes.Lesen);

        erstellt.Klartext.ShouldStartWith("brk_");
        erstellt.Klartext.Length.ShouldBe(44);
        erstellt.Token.Endung.ShouldBe(erstellt.Klartext[^4..]);
        var gespeichert = await LadeAsync(erstellt.Token.Id);
        gespeichert.Hash.ShouldBe(TokenDienst.HashVon(erstellt.Klartext));
        gespeichert.Hash.ShouldNotContain(erstellt.Klartext[4..]);
        gespeichert.Name.ShouldBe("Claude");
        gespeichert.Scopes.ShouldBe([TokenScopes.Lesen]);
    }

    [Fact]
    public async Task Liste_zeigt_nur_eigene_Tokens_neueste_zuerst_ohne_Klartext()
    {
        var benutzer = fabrik.ErzeugeClient(Rolle.Benutzer);
        await ErzeugeAsync(benutzer, "Alt", TokenScopes.Lesen);
        fabrik.Zeit.Vorspulen(TimeSpan.FromMinutes(1));
        await ErzeugeAsync(benutzer, "Neu", TokenScopes.Lesen);
        await ErzeugeAsync(fabrik.ErzeugeClient(Rolle.Admin), "Fremd", TokenScopes.Lesen);

        var antwort = await benutzer.GetAsync(ApiRouten.Tokens.Liste);
        string json = await antwort.Content.ReadAsStringAsync();
        var liste = await antwort.Content.ReadFromJsonAsync<ApiTokenDto[]>();

        liste.ShouldNotBeNull().Select(t => t.Name).ShouldBe(["Neu", "Alt"]);
        json.ShouldNotContain("brk_");
        json.ShouldNotContain("klartext", Case.Insensitive);
    }

    [Fact]
    public async Task Leere_Scopes_ergeben_400()
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer)
            .PostAsJsonAsync(ApiRouten.Tokens.Liste, new ApiTokenAnlegenDto("Leer", []));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unbekannter_Scope_und_fehlender_Name_ergeben_400()
    {
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        (await client.PostAsJsonAsync(ApiRouten.Tokens.Liste, new ApiTokenAnlegenDto("X", ["admin"])))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(ApiRouten.Tokens.Liste, new ApiTokenAnlegenDto(" ", [TokenScopes.Lesen])))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(ApiRouten.Tokens.Liste, new ApiTokenAnlegenDto(new string('a', 101), [TokenScopes.Lesen])))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Gleicher_Name_unter_aktiven_ergibt_409_nach_Widerruf_erlaubt()
    {
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);
        var erstes = await ErzeugeAsync(client, "Doppelt", TokenScopes.Lesen);

        (await client.PostAsJsonAsync(ApiRouten.Tokens.Liste, new ApiTokenAnlegenDto("Doppelt", [TokenScopes.Lesen])))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await WiderrufeAsync(client, erstes.Token.Id, HttpStatusCode.NoContent);
        await ErzeugeAsync(client, "Doppelt", TokenScopes.Lesen);
    }

    [Fact]
    public async Task NurLesen_darf_nur_Tokens_mit_Scope_lesen_anlegen()
    {
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await client.PostAsJsonAsync(ApiRouten.Tokens.Liste, new ApiTokenAnlegenDto("Upload", [TokenScopes.Upload])))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ErzeugeAsync(client, "Lesen", TokenScopes.Lesen);
    }

    [Fact]
    public async Task Widerrufen_ist_idempotent_fremder_Token_404_als_Admin_erlaubt()
    {
        var erstellt = await ErzeugeAsync(fabrik.ErzeugeClient(Rolle.Admin), "Admin-Token", TokenScopes.Lesen);
        var eigenes = await ErzeugeAsync(fabrik.ErzeugeClient(Rolle.Benutzer), "Eigenes", TokenScopes.Lesen);

        await WiderrufeAsync(fabrik.ErzeugeClient(Rolle.Benutzer), erstellt.Token.Id, HttpStatusCode.NotFound);
        (await LadeAsync(erstellt.Token.Id)).WiderrufenAm.ShouldBeNull();

        await WiderrufeAsync(fabrik.ErzeugeClient(Rolle.Admin), eigenes.Token.Id, HttpStatusCode.NoContent);
        var zeitpunkt = (await LadeAsync(eigenes.Token.Id)).WiderrufenAm.ShouldNotBeNull();
        fabrik.Zeit.Vorspulen(TimeSpan.FromMinutes(5));
        await WiderrufeAsync(fabrik.ErzeugeClient(Rolle.Benutzer), eigenes.Token.Id, HttpStatusCode.NoContent);
        (await LadeAsync(eigenes.Token.Id)).WiderrufenAm.ShouldBe(zeitpunkt);
    }

    [Fact]
    public async Task Mcp_akzeptiert_gueltigen_Token_mit_Claims_und_setzt_ZuletztGenutztAm()
    {
        var erstellt = await ErzeugeAsync(fabrik.ErzeugeClient(Rolle.Benutzer), "Claude", TokenScopes.Lesen);
        var client = McpClient(erstellt.Klartext);

        var antwort = await client.GetAsync(McpLesenPfad);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var claims = (await antwort.Content.ReadFromJsonAsync<Dictionary<string, string>>()).ShouldNotBeNull();
        claims[ClaimTypes.NameIdentifier].ShouldBe(ApiFabrik.BenutzerId(Rolle.Benutzer).ToString());
        claims[ClaimTypes.Role].ShouldBe(nameof(Rolle.Benutzer));
        claims["token_id"].ShouldBe(erstellt.Token.Id.ToString());
        claims["token_name"].ShouldBe("Claude");
        claims["scope"].ShouldBe(TokenScopes.Lesen);
        (await LadeAsync(erstellt.Token.Id)).ZuletztGenutztAm.ShouldBe(fabrik.Zeit.GetUtcNow());
    }

    [Fact]
    public async Task ZuletztGenutztAm_wird_hoechstens_einmal_je_Minute_aktualisiert()
    {
        var erstellt = await ErzeugeAsync(fabrik.ErzeugeClient(Rolle.Benutzer), "Claude", TokenScopes.Lesen);
        var client = McpClient(erstellt.Klartext);
        var erste = fabrik.Zeit.GetUtcNow();

        await client.GetAsync(McpLesenPfad);
        fabrik.Zeit.Vorspulen(TimeSpan.FromSeconds(30));
        await client.GetAsync(McpLesenPfad);
        (await LadeAsync(erstellt.Token.Id)).ZuletztGenutztAm.ShouldBe(erste);

        fabrik.Zeit.Vorspulen(TimeSpan.FromSeconds(31));
        await client.GetAsync(McpLesenPfad);
        (await LadeAsync(erstellt.Token.Id)).ZuletztGenutztAm.ShouldBe(fabrik.Zeit.GetUtcNow());
    }

    [Fact]
    public async Task Mcp_lehnt_widerrufenen_unbekannten_und_fehlenden_Token_ab()
    {
        var benutzer = fabrik.ErzeugeClient(Rolle.Benutzer);
        var erstellt = await ErzeugeAsync(benutzer, "Claude", TokenScopes.Lesen);
        await WiderrufeAsync(benutzer, erstellt.Token.Id, HttpStatusCode.NoContent);

        (await McpClient(erstellt.Klartext).GetAsync(McpLesenPfad)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await McpClient("brk_" + new string('x', 40)).GetAsync(McpLesenPfad)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await McpClient(null).GetAsync(McpLesenPfad)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task McpUpload_verlangt_Scope_upload()
    {
        var benutzer = fabrik.ErzeugeClient(Rolle.Benutzer);
        var nurLesen = await ErzeugeAsync(benutzer, "Lesen", TokenScopes.Lesen);
        var upload = await ErzeugeAsync(benutzer, "Upload", TokenScopes.Lesen, TokenScopes.Upload);

        (await McpClient(nurLesen.Klartext).GetAsync(McpUploadPfad)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await McpClient(upload.Klartext).GetAsync(McpUploadPfad)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_oeffnet_die_REST_API_nicht()
    {
        var erstellt = await ErzeugeAsync(fabrik.ErzeugeClient(Rolle.Admin), "Claude", TokenScopes.Lesen, TokenScopes.Upload);

        var antwort = await McpClient(erstellt.Klartext).GetAsync(ApiRouten.Tokens.Liste);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_eines_deaktivierten_oder_gesperrten_Benutzers_ist_ungueltig()
    {
        var erstellt = await ErzeugeAsync(fabrik.ErzeugeClient(Rolle.Benutzer), "Claude", TokenScopes.Lesen);
        var client = McpClient(erstellt.Klartext);
        (await client.GetAsync(McpLesenPfad)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await AendereBenutzerAsync(b => b.Freigegeben = false);
        (await client.GetAsync(McpLesenPfad)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await AendereBenutzerAsync(b =>
        {
            b.Freigegeben = true;
            b.LockoutEnd = fabrik.Zeit.GetUtcNow().AddDays(1);
        });
        (await client.GetAsync(McpLesenPfad)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private ApiFabrik? _mcpFabrik;

    /// <summary>Fabrik mit echter Anmeldung und den Testendpunkten; ihre Uhr läuft mit <see cref="ApiFabrik.Zeit"/> der Basis.</summary>
    private ApiFabrik McpFabrik => _mcpFabrik ??= fabrik
        .MitDiensten(d =>
        {
            d.AddSingleton<IEndpunktModul, McpTestEndpunkte>();
            d.AddSingleton<TimeProvider>(fabrik.Zeit);
        })
        .MitEchterAnmeldung();

    private HttpClient McpClient(string? klartext)
    {
        var client = McpFabrik.CreateClient();
        if (klartext is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", klartext);
        }

        return client;
    }

    private static async Task<ApiTokenErstelltDto> ErzeugeAsync(HttpClient client, string name, params string[] scopes)
    {
        var antwort = await client.PostAsJsonAsync(ApiRouten.Tokens.Liste, new ApiTokenAnlegenDto(name, scopes));
        antwort.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await antwort.Content.ReadFromJsonAsync<ApiTokenErstelltDto>()).ShouldNotBeNull();
    }

    private static async Task WiderrufeAsync(HttpClient client, Guid id, HttpStatusCode erwartet)
    {
        var antwort = await client.PostAsync(ApiRouten.Tokens.Widerrufen.Replace("{id}", id.ToString()), null);
        antwort.StatusCode.ShouldBe(erwartet);
    }

    private async Task<ApiToken> LadeAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Set<ApiToken>().AsNoTracking().SingleAsync(t => t.Id == id);
    }

    private async Task AendereBenutzerAsync(Action<Benutzer> aendern)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var benutzer = await db.Users.SingleAsync(b => b.Id == ApiFabrik.BenutzerId(Rolle.Benutzer));
        aendern(benutzer);
        await db.SaveChangesAsync();
    }

    private async Task LegeBenutzerAnAsync(Rolle rolle)
    {
        var benutzer = Testdaten.Benutzer();
        benutzer.Id = ApiFabrik.BenutzerId(rolle);
        await fabrik.SpeichernAsync(benutzer);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        // Das Zurücksetzen leert auch die per Seed angelegten Rollen.
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

    /// <summary>Testendpunkte unter <c>/mcp</c>; liefern die Claims des angemeldeten Tokens.</summary>
    private sealed class McpTestEndpunkte : IEndpunktModul
    {
        public void Zuordnen(IEndpointRouteBuilder app)
        {
            app.MapGet(McpLesenPfad, Claims).RequireAuthorization(McpRichtlinien.McpLesen);
            app.MapGet(McpUploadPfad, Claims).RequireAuthorization(McpRichtlinien.McpUpload);
        }

        private static IResult Claims(ClaimsPrincipal benutzer) =>
            TypedResults.Ok(benutzer.Claims
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => string.Join(",", g.Select(c => c.Value))));
    }
}
