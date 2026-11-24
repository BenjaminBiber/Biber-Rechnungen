using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Konto;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.IntegrationTests.Infrastruktur;

namespace Biber.Invoice.IntegrationTests.Auth;

[Collection("Api")]
public sealed class KontoTests(ApiFabrik basis) : IAsyncLifetime
{
    private readonly ApiFabrik _fabrik = basis.MitEchterAnmeldung();

    public Task InitializeAsync() => _fabrik.VorbereitenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Andere_abmelden_beendet_die_zweite_Sitzung()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var erste = await _fabrik.AngemeldetAsync(benutzer);
        var zweite = await _fabrik.AngemeldetAsync(benutzer);
        (await KontoAsync(erste)).AktiveSitzungen.ShouldBe(2);

        (await erste.PostAsync(ApiRouten.Konto.AndereSitzungenAbmelden, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await zweite.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await KontoAsync(erste)).AktiveSitzungen.ShouldBe(1);
    }

    [Fact]
    public async Task Konto_zeigt_eigene_Daten_und_Name_und_Email_sind_aenderbar()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.NurLesen);
        var client = await _fabrik.AngemeldetAsync(benutzer);

        var konto = await KontoAsync(client);
        konto.Email.ShouldBe(benutzer.Email);
        konto.TotpAktiv.ShouldBeFalse();
        konto.GoogleVerknuepft.ShouldBeFalse();
        konto.AktiveSitzungen.ShouldBe(1);

        var antwort = await client.PutAsJsonAsync(
            ApiRouten.Konto.Eigenes, new KontoAendernDto("Neuer Name", "neu@example.org"), AuthTestHilfen.Json);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var geaendert = await KontoAsync(client);
        geaendert.Name.ShouldBe("Neuer Name");
        geaendert.Email.ShouldBe("neu@example.org");
        (await _fabrik.Client().AnmeldenAsync("neu@example.org")).ShouldBe(AnmeldeStatus.Ok);
    }

    [Fact]
    public async Task Passwort_aendern_braucht_das_alte_Passwort()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var client = await _fabrik.AngemeldetAsync(benutzer);
        const string neu = "ganz-neues-passwort-2026";

        var falsch = await client.PostAsJsonAsync(
            ApiRouten.Konto.Passwort, new PasswortAendernDto("falsches-altes-passwort", neu), AuthTestHilfen.Json);
        var kurz = await client.PostAsJsonAsync(
            ApiRouten.Konto.Passwort, new PasswortAendernDto(AuthTestHilfen.Passwort, "kurz"), AuthTestHilfen.Json);
        var richtig = await client.PostAsJsonAsync(
            ApiRouten.Konto.Passwort, new PasswortAendernDto(AuthTestHilfen.Passwort, neu), AuthTestHilfen.Json);

        falsch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        kurz.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        richtig.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await KontoAsync(client)).PasswortGeaendertAm.ShouldNotBeNull();
        (await _fabrik.Client().AnmeldenAsync(benutzer.Email!, neu)).ShouldBe(AnmeldeStatus.Ok);
        (await _fabrik.Client().AnmeldenAsync(benutzer.Email!)).ShouldBe(AnmeldeStatus.Ungueltig);
    }

    [Fact]
    public async Task Totp_einrichten_aktivieren_und_deaktivieren()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var client = await _fabrik.AngemeldetAsync(benutzer);

        var einrichtung = await (await client.PostAsync(ApiRouten.Konto.TotpEinrichten, null))
            .Content.ReadFromJsonAsync<TotpEinrichtungDto>(AuthTestHilfen.Json);
        einrichtung.ShouldNotBeNull().OtpAuthUri.ShouldStartWith($"otpauth://totp/Biber-Rechnungen:{benutzer.Email}?secret=");
        var falsch = await client.PostAsJsonAsync(
            ApiRouten.Konto.TotpAktivieren, new CodeDto(AuthTestHilfen.TotpCode(einrichtung.Schluessel, schritte: 20)), AuthTestHilfen.Json);
        falsch.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var aktiviert = await client.PostAsJsonAsync(
            ApiRouten.Konto.TotpAktivieren, new CodeDto(AuthTestHilfen.TotpCode(einrichtung.Schluessel)), AuthTestHilfen.Json);

        aktiviert.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await aktiviert.Content.ReadFromJsonAsync<WiederherstellungscodesDto>(AuthTestHilfen.Json))
            .ShouldNotBeNull().Codes.Length.ShouldBe(8);
        var konto = await KontoAsync(client);
        konto.TotpAktiv.ShouldBeTrue();
        konto.Wiederherstellungscodes.ShouldBe(8);
        (await _fabrik.Client().AnmeldenAsync(benutzer.Email!)).ShouldBe(AnmeldeStatus.TotpErforderlich);

        (await client.PostAsync(ApiRouten.Konto.TotpDeaktivieren, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await KontoAsync(client)).TotpAktiv.ShouldBeFalse();
        (await _fabrik.Client().AnmeldenAsync(benutzer.Email!)).ShouldBe(AnmeldeStatus.Ok);
    }

    private static async Task<KontoDto> KontoAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<KontoDto>(ApiRouten.Konto.Eigenes, AuthTestHilfen.Json)).ShouldNotBeNull();
}
