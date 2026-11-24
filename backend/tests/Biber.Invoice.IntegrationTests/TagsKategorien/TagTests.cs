using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.TagsKategorien;

[Collection("Api")]
public sealed class TagTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly MatchingDto OhneRegel = new(MatchingAlgorithmus.Keiner, null, false);

    public Task InitializeAsync() => fabrik.ZuruecksetzenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient Client => fabrik.ErzeugeClient(Rolle.Benutzer);

    private static TagAendernDto Dto(string name, bool inbox = false, bool zahlung = false, MatchingDto? matching = null) =>
        new(name, "gruen", inbox, zahlung, matching ?? OhneRegel);

    private async Task<TagDto[]> ListeAsync() =>
        (await Client.GetFromJsonAsync<TagDto[]>(ApiRouten.Tags.Liste)).ShouldNotBeNull();

    private async Task<TagDto> AnlegenAsync(TagAendernDto dto)
    {
        var antwort = await Client.PostAsJsonAsync(ApiRouten.Tags.Liste, dto);
        antwort.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await antwort.Content.ReadFromJsonAsync<TagDto>()).ShouldNotBeNull();
    }

    private static string Einzeln(Guid id) => ApiRouten.Tags.Einzeln.Replace("{id}", id.ToString());

    [Fact]
    public async Task Seed_Tags_stehen_vorne_in_der_Liste()
    {
        await AnlegenAsync(Dto("Alpha"));

        var liste = await ListeAsync();

        liste.Select(t => t.Name).ShouldBe(["Inbox", "zu prüfen", "Alpha"]);
        liste[1].IstSystem.ShouldBeTrue();
    }

    [Fact]
    public async Task Anlegen_Aendern_Loeschen()
    {
        var tag = await AnlegenAsync(Dto("Reise"));

        var geaendert = await Client.PutAsJsonAsync(Einzeln(tag.Id), Dto("Reisen", zahlung: true));
        geaendert.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await geaendert.Content.ReadFromJsonAsync<TagDto>()).ShouldNotBeNull();
        dto.Name.ShouldBe("Reisen");
        dto.IstZahlungsTag.ShouldBeTrue();

        (await Client.DeleteAsync(Einzeln(tag.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListeAsync()).ShouldNotContain(t => t.Id == tag.Id);
    }

    [Fact]
    public async Task Loeschen_entfernt_Zuordnungen_und_Zaehler_stimmt()
    {
        var tag = await AnlegenAsync(Dto("Bahn"));
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());
        await fabrik.SpeichernAsync(new BelegTag { BelegId = beleg.Id, TagId = tag.Id, GesetztAm = DateTimeOffset.UtcNow });

        (await ListeAsync()).Single(t => t.Id == tag.Id).AnzahlBelege.ShouldBe(1);
        (await Client.DeleteAsync(Einzeln(tag.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.BelegTags.CountAsync(bt => bt.BelegId == beleg.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Doppelter_Name_in_anderer_Schreibweise_ergibt_409()
    {
        await AnlegenAsync(Dto("Telefon"));

        var antwort = await Client.PostAsJsonAsync(ApiRouten.Tags.Liste, Dto("TELEFON"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Inbox_und_Zahlung_zugleich_ergibt_400()
    {
        var antwort = await Client.PostAsJsonAsync(ApiRouten.Tags.Liste, Dto("Beides", inbox: true, zahlung: true));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Zweiter_Inbox_Tag_nimmt_dem_ersten_das_Kennzeichen()
    {
        var neu = await AnlegenAsync(Dto("Eingang", inbox: true));

        var liste = await ListeAsync();

        liste.Where(t => t.IstInbox).ShouldHaveSingleItem().Id.ShouldBe(neu.Id);
        liste.Single(t => t.Id == SystemIds.TagInbox).IstInbox.ShouldBeFalse();
    }

    [Fact]
    public async Task System_Tag_aendern_oder_loeschen_ergibt_409()
    {
        (await Client.PutAsJsonAsync(Einzeln(SystemIds.TagZuPruefen), Dto("anders")))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Client.DeleteAsync(Einzeln(SystemIds.TagZuPruefen)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Inbox_Tag_ist_loeschbar()
    {
        (await Client.DeleteAsync(Einzeln(SystemIds.TagInbox))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ListeAsync()).ShouldNotContain(t => t.IstInbox);
    }

    [Fact]
    public async Task Ungueltiger_Regex_ergibt_400_mit_Feld()
    {
        var antwort = await Client.PostAsJsonAsync(ApiRouten.Tags.Liste,
            Dto("Regex", matching: new MatchingDto(MatchingAlgorithmus.Regex, "([a-z", false)));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await antwort.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.Keys.ShouldContain("matching.muster");
    }

    [Fact]
    public async Task Fehlendes_Muster_ergibt_400()
    {
        var antwort = await Client.PostAsJsonAsync(ApiRouten.Tags.Liste,
            Dto("Leer", matching: new MatchingDto(MatchingAlgorithmus.EinWort, " ", false)));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Muster_wird_bei_Keiner_geleert()
    {
        var tag = await AnlegenAsync(Dto("Ohne", matching: new MatchingDto(MatchingAlgorithmus.Keiner, "rest", true)));

        tag.Matching.Muster.ShouldBeNull();
        (await ListeAsync()).Single(t => t.Id == tag.Id).Matching.Muster.ShouldBeNull();
    }

    [Fact]
    public async Task NurLesen_darf_lesen_aber_nicht_aendern()
    {
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await client.GetAsync(ApiRouten.Tags.Liste)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(ApiRouten.Tags.Liste, Dto("Verboten"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync(Einzeln(SystemIds.TagInbox), Dto("Verboten"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync(Einzeln(SystemIds.TagInbox))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
