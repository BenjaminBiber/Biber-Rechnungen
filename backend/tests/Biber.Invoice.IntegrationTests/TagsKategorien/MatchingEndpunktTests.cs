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
public sealed class MatchingEndpunktTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly string Fuelltext = new('x', 200);

    public Task InitializeAsync() => fabrik.ZuruecksetzenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient Client => fabrik.ErzeugeClient(Rolle.Benutzer);

    private static MatchingDto EinWort(string muster) => new(MatchingAlgorithmus.EinWort, muster, false);

    private async Task<MatchingTestErgebnisDto> TestenAsync(MatchingTestDto dto)
    {
        var antwort = await Client.PostAsJsonAsync(ApiRouten.Matching.Testen, dto);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await antwort.Content.ReadFromJsonAsync<MatchingTestErgebnisDto>()).ShouldNotBeNull();
    }

    [Fact]
    public async Task Test_liefert_Treffer_Auszug_und_Position()
    {
        await fabrik.SpeichernAsync(Testdaten.Beleg("RE-ALT", new DateOnly(2026, 1, 1), volltext: "Telekom Mobilfunk"));
        await fabrik.SpeichernAsync(Testdaten.Beleg("RE-NEU", new DateOnly(2026, 3, 1), volltext: $"{Fuelltext} Rechnung Mobilfunk Mai {Fuelltext}"));
        await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Strom"));
        await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Mobilfunk", status: BelegStatus.InVerarbeitung));
        var dublette = Testdaten.Beleg(volltext: "Mobilfunk");
        await fabrik.SpeichernAsync(dublette);
        var kopie = Testdaten.Beleg(volltext: "Mobilfunk");
        kopie.DuplikatVonId = dublette.Id;
        await fabrik.SpeichernAsync(kopie);

        var ergebnis = await TestenAsync(new MatchingTestDto("Tag", null, EinWort("mobilfunk")));

        ergebnis.Treffer.ShouldBe(3);
        ergebnis.Gesamt.ShouldBe(4);
        ergebnis.OhneZuweisung.ShouldBe(3);
        ergebnis.LetzterTreffer.ShouldBe("RE-NEU");
        var auszug = ergebnis.Auszug.ShouldNotBeNull();
        auszug.Length.ShouldBe(120);
        auszug.Substring(ergebnis.TrefferStart!.Value, ergebnis.TrefferLaenge!.Value).ShouldBe("Mobilfunk");
    }

    [Fact]
    public async Task Auto_und_Keiner_liefern_nichts()
    {
        await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Mobilfunk"));

        var ergebnis = await TestenAsync(new MatchingTestDto("Kategorie", null, new MatchingDto(MatchingAlgorithmus.Auto, null, false)));

        ergebnis.ShouldBe(new MatchingTestErgebnisDto(0, 0, 0, null, null, null, null));
    }

    [Fact]
    public async Task OhneZuweisung_zaehlt_Treffer_ohne_den_Tag()
    {
        var tag = await fabrik.SpeichernAsync(Testdaten.Tag());
        var mit = Testdaten.Beleg(volltext: "Hosting");
        mit.Tags.Add(new BelegTag { BelegId = mit.Id, TagId = tag.Id, GesetztAm = fabrik.Zeit.GetUtcNow() });
        await fabrik.SpeichernAsync(mit);
        await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Hosting"));
        await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Hosting"));

        var ergebnis = await TestenAsync(new MatchingTestDto("Tag", tag.Id, EinWort("Hosting")));

        ergebnis.Treffer.ShouldBe(3);
        ergebnis.OhneZuweisung.ShouldBe(2);
    }

    [Fact]
    public async Task Anwenden_setzt_den_Tag_auf_genau_die_passenden_Belege()
    {
        var tag = Testdaten.Tag();
        tag.Matching = new Matching { Algorithmus = MatchingAlgorithmus.EinWort, Muster = "Hosting" };
        await fabrik.SpeichernAsync(tag);
        var a = await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Hosting Januar"));
        var b = await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Hosting Februar"));
        await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Strom"));

        var antwort = await Client.PostAsJsonAsync(ApiRouten.Matching.Anwenden, new MatchingAnwendenDto("Tag", tag.Id));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.ReadFromJsonAsync<AnzahlGeaendertDto>())!.Geaendert.ShouldBe(2);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.BelegTags.Where(t => t.TagId == tag.Id).Select(t => t.BelegId).ToListAsync()).ShouldBe([a.Id, b.Id], ignoreOrder: true);

        var nochmal = await Client.PostAsJsonAsync(ApiRouten.Matching.Anwenden, new MatchingAnwendenDto("Tag", tag.Id));
        (await nochmal.Content.ReadFromJsonAsync<AnzahlGeaendertDto>())!.Geaendert.ShouldBe(0);
    }

    [Fact]
    public async Task Anwenden_auf_Kategorie_laesst_gesetzte_und_manuelle_Kategorien()
    {
        var andere = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var kategorie = Testdaten.Kategorie();
        kategorie.Matching = new Matching { Algorithmus = MatchingAlgorithmus.Exakt, Muster = "Hosting" };
        await fabrik.SpeichernAsync(kategorie);
        var leer = await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Hosting"));
        await fabrik.SpeichernAsync(Testdaten.Beleg(kategorieId: andere.Id, volltext: "Hosting"));

        var antwort = await Client.PostAsJsonAsync(ApiRouten.Matching.Anwenden, new MatchingAnwendenDto("Kategorie", kategorie.Id));

        (await antwort.Content.ReadFromJsonAsync<AnzahlGeaendertDto>())!.Geaendert.ShouldBe(1);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Belege.Where(x => x.KategorieId == kategorie.Id).Select(x => x.Id).ToListAsync()).ShouldBe([leer.Id]);
    }

    [Fact]
    public async Task Anwenden_auf_Korrespondent_ergibt_400()
    {
        var antwort = await Client.PostAsJsonAsync(ApiRouten.Matching.Anwenden, new MatchingAnwendenDto("Korrespondent", Guid.NewGuid()));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ungueltiger_Regex_ergibt_400()
    {
        var antwort = await Client.PostAsJsonAsync(ApiRouten.Matching.Testen,
            new MatchingTestDto("Tag", null, new MatchingDto(MatchingAlgorithmus.Regex, "([kaputt", false)));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await antwort.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors.Keys.ShouldContain("matching.muster");
    }

    [Fact]
    public async Task NurLesen_ergibt_403()
    {
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await client.PostAsJsonAsync(ApiRouten.Matching.Testen, new MatchingTestDto("Tag", null, EinWort("x"))))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync(ApiRouten.Matching.Anwenden, new MatchingAnwendenDto("Tag", Guid.NewGuid())))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
