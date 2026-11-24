using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Benutzer;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Auth;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Registrierung;

[Collection("Api")]
public sealed class RegistrierungTests(ApiFabrik basis) : IAsyncLifetime
{
    private const string NeueEmail = "neu@example.org";

    private readonly ApiFabrik _fabrik = basis.MitEchterAnmeldung();

    public async Task InitializeAsync()
    {
        await _fabrik.VorbereitenAsync();
        await _fabrik.BenutzerAnlegenAsync(Rolle.Admin, email: "admin@example.org");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Geschlossen_liefert_403_und_der_Status_meldet_RegistrierungOffen_false()
    {
        var client = _fabrik.Client();

        (await Registrieren(client)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var status = await client.GetJsonAsync<AuthStatusDto>(ApiRouten.Auth.Status);
        status.ShouldNotBeNull().RegistrierungOffen.ShouldBeFalse();
        (await BenutzerAsync(NeueEmail)).ShouldBeNull();
    }

    [Fact]
    public async Task Offen_meldet_der_Status_RegistrierungOffen_true()
    {
        await RegistrierungOeffnenAsync();

        var status = await _fabrik.Client().GetJsonAsync<AuthStatusDto>(ApiRouten.Auth.Status);

        status.ShouldNotBeNull().RegistrierungOffen.ShouldBeTrue();
    }

    [Fact]
    public async Task Ohne_Ersteinrichtung_liefert_409()
    {
        await _fabrik.ZuruecksetzenAsync();
        await RegistrierungOeffnenAsync();

        (await Registrieren(_fabrik.Client())).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Offen_legt_ein_gesperrtes_Konto_ohne_Rolle_an()
    {
        await RegistrierungOeffnenAsync();
        var client = _fabrik.Client();

        (await Registrieren(client)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var benutzer = (await BenutzerAsync(NeueEmail)).ShouldNotBeNull();
        benutzer.Freigegeben.ShouldBeFalse();
        benutzer.Anzeigename.ShouldBe("Neue Person");
        (await RollenAsync(benutzer.Id)).ShouldBeEmpty();
        (await client.AnmeldenAsync(NeueEmail, AuthTestHilfen.Passwort)).ShouldBe(AnmeldeStatus.WartetAufFreigabe);
    }

    [Fact]
    public async Task Admin_sieht_die_Meldung_Rolle_Benutzer_nicht()
    {
        await RegistrierungOeffnenAsync();
        await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer, email: "benutzer@example.org");
        await Registrieren(_fabrik.Client());
        var benutzerId = (await BenutzerAsync(NeueEmail))!.Id;

        var admin = await AdminAsync();
        var normal = await _fabrik.AngemeldetAsync(await BestehendenAsync("benutzer@example.org"));

        var meldung = (await ListeAsync(admin)).ShouldHaveSingleItem();
        meldung.Art.ShouldBe(BenachrichtigungArt.KontoWartetAufFreigabe);
        meldung.Schweregrad.ShouldBe(Schweregrad.Info);
        meldung.Titel.ShouldBe("Neues Konto wartet auf Freigabe");
        meldung.Text.ShouldBe("Neue Person hat sich registriert.");
        meldung.Link.ShouldBe("/einstellungen/benutzer");
        (await ListeAsync(normal)).ShouldBeEmpty();
        using var scope = _fabrik.ErzeugeScope();
        var schluessel = await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Benachrichtigungen
            .Select(b => b.EntdoppelungsSchluessel).SingleAsync();
        schluessel.ShouldBe($"registrierung-{benutzerId}");
    }

    [Fact]
    public async Task Freigeben_mit_Rolle_NurLesen_erlaubt_die_Anmeldung_und_die_Rolle_wirkt()
    {
        await RegistrierungOeffnenAsync();
        await Registrieren(_fabrik.Client());
        var benutzerId = (await BenutzerAsync(NeueEmail))!.Id;
        var admin = await AdminAsync();

        var antwort = await admin.PostJsonAsync(
            Pfad(ApiRouten.Benutzer.Freigeben, benutzerId), new BenutzerFreigebenDto(Rolle.NurLesen));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await antwort.Content.LesenAsync<BenutzerDto>()).ShouldNotBeNull();
        dto.Freigegeben.ShouldBeTrue();
        dto.Rolle.ShouldBe(Rolle.NurLesen);
        var nurLesen = _fabrik.Client();
        (await nurLesen.AnmeldenAsync(NeueEmail)).ShouldBe(AnmeldeStatus.Ok);
        (await nurLesen.GetAsync(ApiRouten.Benutzer.Liste)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await nurLesen.GetAsync(ApiRouten.Benachrichtigungen.Zaehler)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var eintrag = (await AuditAsync(benutzerId)).ShouldHaveSingleItem();
        eintrag.Aktion.ShouldBe("Freigegeben");
        eintrag.Entitaet.ShouldBe("Benutzer");
    }

    [Fact]
    public async Task Ablehnen_entfernt_den_Benutzer()
    {
        await RegistrierungOeffnenAsync();
        await Registrieren(_fabrik.Client());
        var benutzerId = (await BenutzerAsync(NeueEmail))!.Id;
        var admin = await AdminAsync();

        var antwort = await admin.PostAsync(Pfad(ApiRouten.Benutzer.Ablehnen, benutzerId), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await BenutzerAsync(NeueEmail)).ShouldBeNull();
        (await AuditAsync(benutzerId)).ShouldHaveSingleItem().Aktion.ShouldBe("Abgelehnt");
    }

    [Fact]
    public async Task Doppelte_Email_liefert_202_ohne_zweiten_Benutzer_und_ohne_zweite_Meldung()
    {
        await RegistrierungOeffnenAsync();
        var client = _fabrik.Client();
        await Registrieren(client);

        var nochmal = await Registrieren(client, name: "Andere Person");
        var admin = await Registrieren(client, email: "admin@example.org");

        nochmal.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        admin.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var scope = _fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Users.CountAsync()).ShouldBe(2);
        (await db.Benachrichtigungen.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Doppelte_Email_mit_kurzem_Passwort_liefert_ebenfalls_400()
    {
        await RegistrierungOeffnenAsync();

        var antwort = await Registrieren(_fabrik.Client(), email: "admin@example.org", passwort: "kurz");

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Kurzes_Passwort_liefert_400_mit_Feldfehler()
    {
        await RegistrierungOeffnenAsync();

        var antwort = await Registrieren(_fabrik.Client(), passwort: "elf-zeichen");

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await antwort.Content.LesenAsync<Microsoft.AspNetCore.Http.HttpValidationProblemDetails>())
            .ShouldNotBeNull();
        problem.Errors.ShouldContainKey("passwort");
        (await BenutzerAsync(NeueEmail)).ShouldBeNull();
    }

    [Fact]
    public async Task Freigegebenen_Benutzer_ablehnen_liefert_409()
    {
        var admin = await AdminAsync();
        var vorhanden = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer, email: "aktiv@example.org");

        var antwort = await admin.PostAsync(Pfad(ApiRouten.Benutzer.Ablehnen, vorhanden.Id), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await BenutzerAsync("aktiv@example.org")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Freigegebenen_Benutzer_erneut_freigeben_liefert_409()
    {
        var admin = await AdminAsync();
        var vorhanden = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer, email: "aktiv@example.org");

        var antwort = await admin.PostJsonAsync(
            Pfad(ApiRouten.Benutzer.Freigeben, vorhanden.Id), new BenutzerFreigebenDto(Rolle.Admin));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unbekannter_Benutzer_liefert_404()
    {
        var admin = await AdminAsync();

        (await admin.PostAsync(Pfad(ApiRouten.Benutzer.Ablehnen, Guid.NewGuid()), null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Rolle_Benutzer_darf_weder_freigeben_noch_ablehnen()
    {
        await RegistrierungOeffnenAsync();
        await Registrieren(_fabrik.Client());
        var benutzerId = (await BenutzerAsync(NeueEmail))!.Id;
        await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer, email: "benutzer@example.org");
        var normal = await _fabrik.AngemeldetAsync(await BestehendenAsync("benutzer@example.org"));

        (await normal.PostJsonAsync(
                Pfad(ApiRouten.Benutzer.Freigeben, benutzerId), new BenutzerFreigebenDto(Rolle.Admin)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await normal.PostAsync(Pfad(ApiRouten.Benutzer.Ablehnen, benutzerId), null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await BenutzerAsync(NeueEmail)).ShouldNotBeNull().Freigegeben.ShouldBeFalse();
    }

    [Fact]
    public async Task Die_sechste_Registrierung_in_einer_Stunde_liefert_429()
    {
        await RegistrierungOeffnenAsync();
        var client = _fabrik.Client();

        for (var i = 1; i <= 5; i++)
        {
            (await Registrieren(client, email: $"person{i}@example.org")).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        (await Registrieren(client, email: "person6@example.org")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await BenutzerAsync("person6@example.org")).ShouldBeNull();
    }

    private static string Pfad(string route, Guid id) => route.Replace("{id}", id.ToString(), StringComparison.Ordinal);

    private static Task<HttpResponseMessage> Registrieren(
        HttpClient client,
        string email = NeueEmail,
        string name = "Neue Person",
        string passwort = AuthTestHilfen.Passwort) =>
        client.PostJsonAsync(ApiRouten.Auth.Registrieren, new RegistrierenDto(name, email, passwort));

    private async Task RegistrierungOeffnenAsync()
    {
        using var scope = _fabrik.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<IEinstellungen>()
            .SpeichernAsync(new ZugriffEinstellungen { OffeneRegistrierung = true }, CancellationToken.None);
    }

    private async Task<HttpClient> AdminAsync() => await _fabrik.AngemeldetAsync(await BestehendenAsync("admin@example.org"));

    private async Task<Benutzer> BestehendenAsync(string email) => (await BenutzerAsync(email)).ShouldNotBeNull();

    private async Task<Benutzer?> BenutzerAsync(string email)
    {
        using var scope = _fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Users
            .AsNoTracking().SingleOrDefaultAsync(b => b.Email == email);
    }

    private async Task<List<string>> RollenAsync(Guid benutzerId)
    {
        using var scope = _fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.UserRoles.Where(r => r.UserId == benutzerId).Select(r => r.RoleId.ToString()).ToListAsync();
    }

    private async Task<List<AuditEintrag>> AuditAsync(Guid benutzerId)
    {
        using var scope = _fabrik.ErzeugeScope();
        var kennung = benutzerId.ToString();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().AuditEintraege
            .AsNoTracking().Where(a => a.Entitaet == "Benutzer" && a.EntitaetId == kennung).ToListAsync();
    }

    private static async Task<List<BenachrichtigungDto>> ListeAsync(HttpClient client)
    {
        var seite = await client.GetJsonAsync<SeiteDto<BenachrichtigungDto>>(
            ApiRouten.Benachrichtigungen.Liste);
        return [.. seite.ShouldNotBeNull().Eintraege];
    }
}
