using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Benutzer;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Auth;

[Collection("Api")]
public sealed class BenutzerverwaltungTests(ApiFabrik basis) : IAsyncLifetime
{
    private const string BearbeitenPfad = "/api/test/auth/bearbeiten";

    private readonly ApiFabrik _fabrik = basis
        .MitEchterAnmeldung()
        .MitDiensten(d => d.AddSingleton<IEndpunktModul, BearbeitenEndpunkt>());

    public Task InitializeAsync() => _fabrik.VorbereitenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NurLesen_bekommt_403_auf_einem_Bearbeiten_Endpunkt()
    {
        var nurLesen = await _fabrik.AngemeldetAsync(await _fabrik.BenutzerAnlegenAsync(Rolle.NurLesen));
        var benutzer = await _fabrik.AngemeldetAsync(await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer));

        (await nurLesen.PostAsync(BearbeitenPfad, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await benutzer.PostAsync(BearbeitenPfad, null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Benutzer_bekommt_403_auf_die_Benutzerliste()
    {
        var benutzer = await _fabrik.AngemeldetAsync(await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer));

        var antwort = await benutzer.GetAsync(ApiRouten.Benutzer.Liste);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        antwort.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Admin_legt_Benutzer_an_der_sich_sofort_anmelden_kann()
    {
        var admin = await _fabrik.AngemeldetAsync(await _fabrik.BenutzerAnlegenAsync(Rolle.Admin));

        var antwort = await admin.PostJsonAsync(
            ApiRouten.Benutzer.Liste,
            new BenutzerAnlegenDto("Steuerbüro", "kanzlei@example.org", AuthTestHilfen.Passwort, Rolle.NurLesen));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Created);
        var neu = (await antwort.Content.LesenAsync<BenutzerDto>()).ShouldNotBeNull();
        neu.Rolle.ShouldBe(Rolle.NurLesen);
        neu.Freigegeben.ShouldBeTrue();
        neu.Anmeldung.ShouldBe("Passwort");
        var liste = await admin.GetJsonAsync<BenutzerDto[]>(ApiRouten.Benutzer.Liste);
        liste.ShouldNotBeNull().Length.ShouldBe(2);
        liste.ShouldContain(b => b.Email == "kanzlei@example.org" && b.Rolle == Rolle.NurLesen);
        (await _fabrik.Client().AnmeldenAsync("kanzlei@example.org")).ShouldBe(AnmeldeStatus.Ok);
    }

    [Fact]
    public async Task Passwort_unter_12_Zeichen_liefert_400()
    {
        var admin = await _fabrik.AngemeldetAsync(await _fabrik.BenutzerAnlegenAsync(Rolle.Admin));

        var antwort = await admin.PostJsonAsync(
            ApiRouten.Benutzer.Liste,
            new BenutzerAnlegenDto("Kurz", "kurz@example.org", "elf-zeichen", Rolle.Benutzer));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await antwort.Content.ReadAsStringAsync()).ShouldContain("mindestens 12 Zeichen");
    }

    [Fact]
    public async Task Letzter_aktiver_Admin_kann_weder_herabgestuft_noch_deaktiviert_werden()
    {
        var adminKonto = await _fabrik.BenutzerAnlegenAsync(Rolle.Admin);
        var admin = await _fabrik.AngemeldetAsync(adminKonto);
        var pfad = ApiRouten.Benutzer.Einzeln.Replace("{id}", adminKonto.Id.ToString(), StringComparison.Ordinal);

        (await admin.PutJsonAsync(pfad, new BenutzerAendernDto(Rolle.Benutzer, true)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.PutJsonAsync(pfad, new BenutzerAendernDto(Rolle.Admin, false)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await _fabrik.BenutzerAnlegenAsync(Rolle.Admin);
        var antwort = await admin.PutJsonAsync(pfad, new BenutzerAendernDto(Rolle.Benutzer, true));
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<BenutzerDto>()).ShouldNotBeNull().Rolle.ShouldBe(Rolle.Benutzer);
    }

    [Fact]
    public async Task Deaktivieren_beendet_die_Sitzungen_des_Benutzers()
    {
        var admin = await _fabrik.AngemeldetAsync(await _fabrik.BenutzerAnlegenAsync(Rolle.Admin));
        var konto = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var benutzer = await _fabrik.AngemeldetAsync(konto);
        var pfad = ApiRouten.Benutzer.Einzeln.Replace("{id}", konto.Id.ToString(), StringComparison.Ordinal);

        var antwort = await admin.PutJsonAsync(pfad, new BenutzerAendernDto(Rolle.Benutzer, false));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await benutzer.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _fabrik.Client().AnmeldenAsync(konto.Email!)).ShouldBe(AnmeldeStatus.WartetAufFreigabe);
    }

    [Fact]
    public async Task Admin_setzt_Passwort_zurueck()
    {
        var admin = await _fabrik.AngemeldetAsync(await _fabrik.BenutzerAnlegenAsync(Rolle.Admin));
        var konto = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var pfad = ApiRouten.Benutzer.Passwort.Replace("{id}", konto.Id.ToString(), StringComparison.Ordinal);
        const string neu = "vom-admin-gesetzt-2026";

        (await admin.PostJsonAsync(pfad, new NeuesPasswortDto("kurz")))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync(pfad, new NeuesPasswortDto(neu)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.PostJsonAsync(
                ApiRouten.Benutzer.Passwort.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal),
                new NeuesPasswortDto(neu)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await _fabrik.Client().AnmeldenAsync(konto.Email!, neu)).ShouldBe(AnmeldeStatus.Ok);
    }

    private sealed class BearbeitenEndpunkt : IEndpunktModul
    {
        public void Zuordnen(IEndpointRouteBuilder app) =>
            app.MapPost(BearbeitenPfad, () => "ok").RequireAuthorization(Richtlinien.Bearbeiten);
    }
}
