using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Konto;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Auth;
using Biber.Invoice.IntegrationTests.Infrastruktur;

namespace Biber.Invoice.IntegrationTests.Auth;

[Collection("Api")]
public sealed class AnmeldungTests(ApiFabrik basis) : IAsyncLifetime
{
    private readonly ApiFabrik _fabrik = basis.MitEchterAnmeldung();

    public Task InitializeAsync() => _fabrik.VorbereitenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Ersteinrichtung_legt_angemeldeten_Admin_an_und_ist_nur_einmal_moeglich()
    {
        var client = _fabrik.Client();
        (await StatusAsync(client)).Eingerichtet.ShouldBeFalse();
        var vorschlag = await client.GetFromJsonAsync<TotpEinrichtungDto>(ApiRouten.Auth.TotpVorschlag, AuthTestHilfen.Json);
        vorschlag.ShouldNotBeNull().Schluessel.Length.ShouldBe(32);
        vorschlag.OtpAuthUri.ShouldBe($"otpauth://totp/Biber-Rechnungen:Admin?secret={vorschlag.Schluessel}&issuer=Biber-Rechnungen");

        var antwort = await client.PostAsJsonAsync(
            ApiRouten.Auth.Ersteinrichtung,
            new ErsteinrichtungDto("Martin Keller", "m.keller@firma.de", AuthTestHilfen.Passwort, vorschlag.Schluessel),
            AuthTestHilfen.Json);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Created);
        var admin = (await antwort.Content.ReadFromJsonAsync<BenutzerDto>(AuthTestHilfen.Json)).ShouldNotBeNull();
        admin.Rolle.ShouldBe(Rolle.Admin);
        admin.Freigegeben.ShouldBeTrue();
        admin.TotpAktiv.ShouldBeTrue();
        admin.Anmeldung.ShouldBe("Passwort");
        var status = await StatusAsync(client);
        status.Eingerichtet.ShouldBeTrue();
        status.Angemeldet.ShouldBeTrue();
        status.Benutzer.ShouldNotBeNull().Email.ShouldBe("m.keller@firma.de");
        status.RegistrierungOffen.ShouldBeFalse();
        status.GoogleAktiv.ShouldBeFalse();

        var zweite = await _fabrik.Client().PostAsJsonAsync(
            ApiRouten.Auth.Ersteinrichtung,
            new ErsteinrichtungDto("Zweiter", "zweiter@firma.de", AuthTestHilfen.Passwort, vorschlag.Schluessel),
            AuthTestHilfen.Json);
        zweite.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.GetAsync(ApiRouten.Auth.TotpVorschlag)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ersteinrichtung_mit_kurzem_Passwort_liefert_400()
    {
        var antwort = await _fabrik.Client().PostAsJsonAsync(
            ApiRouten.Auth.Ersteinrichtung,
            new ErsteinrichtungDto("Martin Keller", "m.keller@firma.de", "zu-kurz", AuthTestHilfen.TotpSchluessel),
            AuthTestHilfen.Json);

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await antwort.Content.ReadAsStringAsync()).ShouldContain("passwort");
        (await StatusAsync(_fabrik.Client())).Eingerichtet.ShouldBeFalse();
    }

    [Fact]
    public async Task Anmeldung_ohne_Totp_Code_liefert_TotpErforderlich_und_noch_kein_gueltiges_Cookie()
    {
        var admin = await _fabrik.BenutzerAnlegenAsync(Rolle.Admin, mitTotp: true);
        var client = _fabrik.Client();

        (await client.AnmeldenAsync(admin.Email!)).ShouldBe(AnmeldeStatus.TotpErforderlich);

        (await client.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusAsync(client)).Angemeldet.ShouldBeFalse();
    }

    [Fact]
    public async Task Gueltiger_Totp_Code_meldet_an()
    {
        var admin = await _fabrik.BenutzerAnlegenAsync(Rolle.Admin, mitTotp: true);
        var client = _fabrik.Client();
        await client.AnmeldenAsync(admin.Email!);

        (await client.TotpAsync(AuthTestHilfen.TotpCode(AuthTestHilfen.TotpSchluessel))).ShouldBe(AnmeldeStatus.Ok);

        (await client.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var status = await StatusAsync(client);
        status.Angemeldet.ShouldBeTrue();
        status.Benutzer.ShouldNotBeNull().Id.ShouldBe(admin.Id);
    }

    [Fact]
    public async Task Falscher_Totp_Code_meldet_nicht_an()
    {
        var admin = await _fabrik.BenutzerAnlegenAsync(Rolle.Admin, mitTotp: true);
        var client = _fabrik.Client();
        await client.AnmeldenAsync(admin.Email!);

        (await client.TotpAsync(AuthTestHilfen.TotpCode(AuthTestHilfen.TotpSchluessel, schritte: 20))).ShouldBe(AnmeldeStatus.Ungueltig);

        (await client.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Totp_ohne_vorherige_Passwortpruefung_ist_ungueltig()
    {
        await _fabrik.BenutzerAnlegenAsync(Rolle.Admin, mitTotp: true);

        (await _fabrik.Client().TotpAsync(AuthTestHilfen.TotpCode(AuthTestHilfen.TotpSchluessel))).ShouldBe(AnmeldeStatus.Ungueltig);
    }

    [Fact]
    public async Task Wiederherstellungscode_funktioniert_genau_einmal()
    {
        var admin = await _fabrik.BenutzerAnlegenAsync(Rolle.Admin, mitTotp: true);
        var angemeldet = await _fabrik.AngemeldetAsync(admin);
        var codes = await (await angemeldet.PostAsync(ApiRouten.Konto.Wiederherstellungscodes, null))
            .Content.ReadFromJsonAsync<WiederherstellungscodesDto>(AuthTestHilfen.Json);
        codes.ShouldNotBeNull().Codes.Length.ShouldBe(8);

        var erster = _fabrik.Client();
        await erster.AnmeldenAsync(admin.Email!);
        (await WiederherstellungscodeAsync(erster, codes.Codes[0])).ShouldBe(AnmeldeStatus.Ok);
        (await erster.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var zweiter = _fabrik.Client();
        await zweiter.AnmeldenAsync(admin.Email!);
        (await WiederherstellungscodeAsync(zweiter, codes.Codes[0])).ShouldBe(AnmeldeStatus.Ungueltig);
        (await zweiter.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var konto = await erster.GetFromJsonAsync<KontoDto>(ApiRouten.Konto.Eigenes, AuthTestHilfen.Json);
        konto.ShouldNotBeNull().Wiederherstellungscodes.ShouldBe(7);
    }

    [Fact]
    public async Task Nach_5_Fehlversuchen_ist_das_Konto_gesperrt()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var client = _fabrik.Client();

        for (var i = 1; i <= 4; i++)
        {
            (await client.AnmeldenAsync(benutzer.Email!, "falsches-passwort-123")).ShouldBe(AnmeldeStatus.Ungueltig);
        }

        (await client.AnmeldenAsync(benutzer.Email!, "falsches-passwort-123")).ShouldBe(AnmeldeStatus.Gesperrt);
        (await client.AnmeldenAsync(benutzer.Email!)).ShouldBe(AnmeldeStatus.Gesperrt);
        (await client.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unbekannte_Email_und_falsches_Passwort_sind_nicht_unterscheidbar()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var client = _fabrik.Client();

        (await client.AnmeldenAsync("niemand@example.org")).ShouldBe(AnmeldeStatus.Ungueltig);
        (await client.AnmeldenAsync(benutzer.Email!, "falsches-passwort-123")).ShouldBe(AnmeldeStatus.Ungueltig);
    }

    [Fact]
    public async Task Nicht_freigegebenes_Konto_wartet_auf_Freigabe()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(rolle: null, freigegeben: false);
        var client = _fabrik.Client();

        (await client.AnmeldenAsync(benutzer.Email!)).ShouldBe(AnmeldeStatus.WartetAufFreigabe);
        (await client.AnmeldenAsync(benutzer.Email!, "falsches-passwort-123")).ShouldBe(AnmeldeStatus.Ungueltig);
        (await client.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Abmelden_macht_das_Cookie_ungueltig()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var client = _fabrik.Client();
        var antwort = await client.PostAsJsonAsync(
            ApiRouten.Auth.Anmelden, new AnmeldenDto(benutzer.Email!, AuthTestHilfen.Passwort, false), AuthTestHilfen.Json);
        var cookie = SitzungsCookie(antwort);

        (await client.PostAsync(ApiRouten.Auth.Abmelden, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var alterCookie = _fabrik.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = false });
        alterCookie.DefaultRequestHeaders.Add("Cookie", cookie);
        (await alterCookie.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cookie_ist_HttpOnly_SameSite_Lax_und_ohne_Angemeldet_bleiben_ein_Sitzungs_Cookie()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);

        var fluechtig = SetCookie(await _fabrik.Client().PostAsJsonAsync(
            ApiRouten.Auth.Anmelden, new AnmeldenDto(benutzer.Email!, AuthTestHilfen.Passwort, false), AuthTestHilfen.Json));
        var dauerhaft = SetCookie(await _fabrik.Client().PostAsJsonAsync(
            ApiRouten.Auth.Anmelden, new AnmeldenDto(benutzer.Email!, AuthTestHilfen.Passwort, true), AuthTestHilfen.Json));

        fluechtig.ShouldContain("httponly", Case.Insensitive);
        fluechtig.ShouldContain("samesite=lax", Case.Insensitive);
        fluechtig.ShouldNotContain("expires", Case.Insensitive);
        dauerhaft.ShouldContain("expires", Case.Insensitive);
    }

    [Fact]
    public async Task Api_Aufruf_ohne_Cookie_liefert_401_ohne_Umleitung()
    {
        var antwort = await _fabrik.Client().GetAsync(ApiRouten.Konto.Eigenes);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        antwort.Headers.Location.ShouldBeNull();
    }

    private static async Task<AuthStatusDto> StatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<AuthStatusDto>(ApiRouten.Auth.Status, AuthTestHilfen.Json)).ShouldNotBeNull();

    private static async Task<AnmeldeStatus> WiederherstellungscodeAsync(HttpClient client, string code)
    {
        var antwort = await client.PostAsJsonAsync(ApiRouten.Auth.Wiederherstellungscode, new CodeDto(code), AuthTestHilfen.Json);
        return (await antwort.Content.ReadFromJsonAsync<AnmeldeErgebnisDto>(AuthTestHilfen.Json))!.Status;
    }

    private static string SetCookie(HttpResponseMessage antwort) =>
        antwort.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(AuthModul.CookieName + "=", StringComparison.Ordinal));

    private static string SitzungsCookie(HttpResponseMessage antwort) => SetCookie(antwort).Split(';')[0];
}
