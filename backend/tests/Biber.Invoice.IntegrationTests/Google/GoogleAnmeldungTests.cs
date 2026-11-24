using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Konto;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Auth;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Biber.Invoice.IntegrationTests.Google;

/// <summary>
/// Anmeldung und Verknüpfung über Google; Token- und UserInfo-Endpunkt von Google bildet WireMock nach.
/// Der Test folgt den Umleitungen selbst und ruft den Rückruf der Middleware mit dem <c>state</c> der Challenge auf.
/// </summary>
[Collection("Api")]
public sealed class GoogleAnmeldungTests(ApiFabrik basis) : IAsyncLifetime
{
    private const string Rueckruf = "/api/auth/google/signin";

    private readonly WireMockServer _google = WireMockServer.Start();
    private readonly ApiFabrik _echt = basis.MitEchterAnmeldung();
    private WebApplicationFactory<Program> _fabrik = null!;

    public async Task InitializeAsync()
    {
        _fabrik = _echt.WithWebHostBuilder(b => b
            .UseSetting("Google:ClientId", "client-id")
            .UseSetting("Google:ClientSecret", "client-secret")
            .UseSetting("Google:AuthorizationEndpunkt", _google.Url + "/auth")
            .UseSetting("Google:TokenEndpunkt", _google.Url + "/token")
            .UseSetting("Google:UserInfoEndpunkt", _google.Url + "/userinfo"));
        await _echt.VorbereitenAsync();
        _google.Given(Request.Create().WithPath("/token").UsingPost())
            .RespondWith(Response.Create().WithBodyAsJson(new { access_token = "token", token_type = "Bearer", expires_in = 3600 }));
        await EinstellenAsync(new ZugriffEinstellungen { GoogleAnmeldung = true });
    }

    public async Task DisposeAsync()
    {
        await _fabrik.DisposeAsync();
        _google.Stop();
    }

    [Fact]
    public async Task Bekannte_Verknuepfung_meldet_ohne_TOTP_an()
    {
        var benutzer = await _echt.BenutzerAnlegenAsync(Rolle.Benutzer, mitTotp: true);
        await VerknuepfenAsync(benutzer, "g-1");
        GoogleMeldet("g-1", "anders@example.org", verifiziert: false);
        var client = Client();

        (await GoogleDurchlaufenAsync(client)).ShouldBe("/");

        (await StatusAsync(client)).Benutzer!.Id.ShouldBe(benutzer.Id);
    }

    [Fact]
    public async Task Verifizierte_Email_eines_freigegebenen_Benutzers_verknuepft_und_meldet_an()
    {
        var benutzer = await _echt.BenutzerAnlegenAsync(Rolle.Benutzer);
        GoogleMeldet("g-2", benutzer.Email!, verifiziert: true);
        var client = Client();

        (await GoogleDurchlaufenAsync(client)).ShouldBe("/");

        (await StatusAsync(client)).Angemeldet.ShouldBeTrue();
        (await KontoAsync(client)).GoogleVerknuepft.ShouldBeTrue();
    }

    [Fact]
    public async Task Nicht_verifizierte_Email_meldet_nicht_an()
    {
        var benutzer = await _echt.BenutzerAnlegenAsync(Rolle.Benutzer);
        GoogleMeldet("g-3", benutzer.Email!, verifiziert: false);
        var client = Client();

        (await GoogleDurchlaufenAsync(client)).ShouldBe("/anmelden?fehler=google-unbekannt");

        (await StatusAsync(client)).Angemeldet.ShouldBeFalse();
    }

    [Fact]
    public async Task Nicht_freigegebener_Benutzer_wartet_auf_Freigabe()
    {
        var benutzer = await _echt.BenutzerAnlegenAsync(Rolle.Benutzer, freigegeben: false);
        GoogleMeldet("g-4", benutzer.Email!, verifiziert: true);
        var client = Client();

        (await GoogleDurchlaufenAsync(client)).ShouldBe("/anmelden?schritt=freigabe");

        (await StatusAsync(client)).Angemeldet.ShouldBeFalse();
    }

    [Fact]
    public async Task Unbekannte_Email_bei_geschlossener_Registrierung_legt_keinen_Benutzer_an()
    {
        GoogleMeldet("g-5", "fremd@example.org", verifiziert: true);
        var client = Client();

        (await GoogleDurchlaufenAsync(client)).ShouldBe("/anmelden?fehler=google-unbekannt");

        (await BenutzerAsync("fremd@example.org")).ShouldBeNull();
        (await StatusAsync(client)).Angemeldet.ShouldBeFalse();
    }

    [Fact]
    public async Task Unbekannte_Email_bei_offener_Registrierung_legt_gesperrten_Benutzer_an()
    {
        await EinstellenAsync(new ZugriffEinstellungen { GoogleAnmeldung = true, OffeneRegistrierung = true });
        GoogleMeldet("g-6", "neu@example.org", verifiziert: true, name: "Neue Person");
        var client = Client();

        (await GoogleDurchlaufenAsync(client)).ShouldBe("/anmelden?schritt=freigabe");

        var neu = (await BenutzerAsync("neu@example.org")).ShouldNotBeNull();
        neu.Freigegeben.ShouldBeFalse();
        neu.Anzeigename.ShouldBe("Neue Person");
        neu.PasswordHash.ShouldBeNull();
        (await StatusAsync(client)).Angemeldet.ShouldBeFalse();
        using var scope = _fabrik.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.UserLogins.AnyAsync(l => l.UserId == neu.Id && l.ProviderKey == "g-6")).ShouldBeTrue();
    }

    [Fact]
    public async Task Verknuepfen_aus_dem_Konto()
    {
        var benutzer = await _echt.BenutzerAnlegenAsync(Rolle.Benutzer);
        GoogleMeldet("g-7", "privat@gmail.example", verifiziert: true);
        var client = await AngemeldetAsync(benutzer);

        (await GoogleDurchlaufenAsync(client, verknuepfen: true)).ShouldBe("/einstellungen/konto?google=verknuepft");

        (await KontoAsync(client)).GoogleVerknuepft.ShouldBeTrue();
    }

    [Fact]
    public async Task Bereits_vergebenes_Google_Konto_wird_nicht_verknuepft()
    {
        var anderer = await _echt.BenutzerAnlegenAsync(Rolle.Benutzer, email: "anderer@example.org");
        await VerknuepfenAsync(anderer, "g-8");
        var benutzer = await _echt.BenutzerAnlegenAsync(Rolle.Benutzer);
        GoogleMeldet("g-8", "anderer@example.org", verifiziert: true);
        var client = await AngemeldetAsync(benutzer);

        (await GoogleDurchlaufenAsync(client, verknuepfen: true)).ShouldBe("/einstellungen/konto?google=vergeben");

        (await KontoAsync(client)).GoogleVerknuepft.ShouldBeFalse();
    }

    [Fact]
    public async Task Verknuepfen_braucht_Anmeldung()
    {
        (await Client().GetAsync(ApiRouten.Auth.GoogleStart + "?verknuepfen=true")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GoogleAktiv_nur_bei_Konfiguration_und_Einstellung()
    {
        (await StatusAsync(Client())).GoogleAktiv.ShouldBeTrue();

        await EinstellenAsync(new ZugriffEinstellungen { GoogleAnmeldung = false });
        (await StatusAsync(Client())).GoogleAktiv.ShouldBeFalse();

        await EinstellenAsync(new ZugriffEinstellungen { GoogleAnmeldung = true });
        var ohneKonfiguration = _echt.Client();
        (await StatusAsync(ohneKonfiguration)).GoogleAktiv.ShouldBeFalse();
        (await ohneKonfiguration.GetAsync(ApiRouten.Auth.GoogleStart)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Start_liefert_404_wenn_abgeschaltet()
    {
        await EinstellenAsync(new ZugriffEinstellungen { GoogleAnmeldung = false });

        (await Client().GetAsync(ApiRouten.Auth.GoogleStart)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ungueltiger_state_meldet_nicht_an()
    {
        var benutzer = await _echt.BenutzerAnlegenAsync(Rolle.Benutzer);
        await VerknuepfenAsync(benutzer, "g-9");
        GoogleMeldet("g-9", benutzer.Email!, verifiziert: true);
        var client = Client();
        (await client.GetAsync(ApiRouten.Auth.GoogleStart)).StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var antwort = await client.GetAsync(Rueckruf + "?code=code&state=gefaelscht");

        antwort.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        antwort.Headers.Location!.OriginalString.ShouldBe("/anmelden?fehler=google-unbekannt");
        (await StatusAsync(client)).Angemeldet.ShouldBeFalse();
    }

    /// <summary>HTTPS, weil das Korrelations-Cookie der Middleware nur über sichere Verbindungen gilt.</summary>
    private HttpClient Client() => _fabrik.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost"),
    });

    private async Task<HttpClient> AngemeldetAsync(Benutzer benutzer)
    {
        var client = Client();
        (await client.AnmeldenAsync(benutzer.Email!)).ShouldBe(AnmeldeStatus.Ok);
        return client;
    }

    /// <summary>start → Google (nur state übernehmen) → Rückruf der Middleware → Rückruf-Endpunkt; liefert das Ziel im Frontend.</summary>
    private static async Task<string> GoogleDurchlaufenAsync(HttpClient client, bool verknuepfen = false)
    {
        var start = await client.GetAsync(ApiRouten.Auth.GoogleStart + (verknuepfen ? "?verknuepfen=true" : ""));
        start.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var zuGoogle = start.Headers.Location!;
        zuGoogle.AbsolutePath.ShouldBe("/auth");
        var state = QueryHelpers.ParseQuery(zuGoogle.Query)["state"].ToString();

        var middleware = await client.GetAsync($"{Rueckruf}?code=code&state={Uri.EscapeDataString(state)}");
        middleware.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var endpunkt = await client.GetAsync(middleware.Headers.Location!.OriginalString);
        endpunkt.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        return endpunkt.Headers.Location!.OriginalString;
    }

    private void GoogleMeldet(string id, string email, bool verifiziert, string name = "Google Person")
    {
        _google.Given(Request.Create().WithPath("/userinfo").UsingGet())
            .RespondWith(Response.Create().WithBodyAsJson(new
            {
                sub = id,
                id,
                email,
                email_verified = verifiziert,
                name,
            }));
    }

    private async Task VerknuepfenAsync(Benutzer benutzer, string googleId)
    {
        using var scope = _fabrik.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>();
        var geladen = await userManager.FindByIdAsync(benutzer.Id.ToString());
        (await userManager.AddLoginAsync(geladen!, new UserLoginInfo("Google", googleId, "Google"))).Succeeded.ShouldBeTrue();
    }

    private async Task EinstellenAsync(ZugriffEinstellungen zugriff)
    {
        using var scope = _fabrik.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IEinstellungen>().SpeichernAsync(zugriff, CancellationToken.None);
    }

    private async Task<Benutzer?> BenutzerAsync(string email)
    {
        using var scope = _fabrik.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<Benutzer>>().FindByEmailAsync(email);
    }

    private static async Task<AuthStatusDto> StatusAsync(HttpClient client) =>
        (await client.GetJsonAsync<AuthStatusDto>(ApiRouten.Auth.Status))!;

    private static async Task<KontoDto> KontoAsync(HttpClient client) =>
        (await client.GetJsonAsync<KontoDto>(ApiRouten.Konto.Eigenes))!;
}
