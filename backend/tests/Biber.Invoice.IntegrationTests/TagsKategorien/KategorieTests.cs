using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.TagsKategorien;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.TagsKategorien;

[Collection("Api")]
public sealed class KategorieTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly MatchingDto OhneRegel = new(MatchingAlgorithmus.Keiner, null, false);

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient Client => fabrik.ErzeugeClient(Rolle.Benutzer);

    private static KategorieAendernDto Dto(string name, string farbe = "#1F8A70", string? euerZeile = null, MatchingDto? matching = null) =>
        new(name, farbe, euerZeile, matching ?? OhneRegel);

    private static string Einzeln(Guid id) => ApiRouten.Kategorien.Einzeln.Replace("{id}", id.ToString());

    private async Task<KategorieDto[]> ListeAsync(int? jahr = null) =>
        (await Client.GetFromJsonAsync<KategorieDto[]>(ApiRouten.Kategorien.Liste + (jahr is null ? "" : $"?jahr={jahr}"))).ShouldNotBeNull();

    private async Task<KategorieDto> AnlegenAsync(KategorieAendernDto dto)
    {
        var antwort = await Client.PostAsJsonAsync(ApiRouten.Kategorien.Liste, dto);
        antwort.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await antwort.Content.ReadFromJsonAsync<KategorieDto>()).ShouldNotBeNull();
    }

    [Fact]
    public async Task Anlegen_speichert_EUeR_Zeile_Aendern_und_Loeschen()
    {
        var kategorie = await AnlegenAsync(Dto("Software", euerZeile: "Zeile 33 – Sonstige Betriebsausgaben"));
        kategorie.EuerZeile.ShouldBe("Zeile 33 – Sonstige Betriebsausgaben");
        (await ListeAsync()).ShouldHaveSingleItem().EuerZeile.ShouldBe("Zeile 33 – Sonstige Betriebsausgaben");

        var geaendert = await Client.PutAsJsonAsync(Einzeln(kategorie.Id), Dto("Software & Lizenzen", euerZeile: "Zeile 30"));
        geaendert.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await geaendert.Content.ReadFromJsonAsync<KategorieDto>()).ShouldNotBeNull();
        dto.Name.ShouldBe("Software & Lizenzen");
        dto.EuerZeile.ShouldBe("Zeile 30");

        (await Client.DeleteAsync(Einzeln(kategorie.Id))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ListeAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Ohne_Farbe_wird_reihum_eine_Designfarbe_vergeben()
    {
        var erste = await AnlegenAsync(Dto("A", farbe: ""));
        var zweite = await AnlegenAsync(Dto("B", farbe: ""));

        erste.Farbe.ShouldBe("#5B8DEF");
        zweite.Farbe.ShouldBe("#1F8A70");
    }

    [Fact]
    public async Task Ungueltige_Eingaben_ergeben_400()
    {
        (await Client.PostAsJsonAsync(ApiRouten.Kategorien.Liste, Dto("Rot", farbe: "rot")))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(ApiRouten.Kategorien.Liste, Dto(" ")))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(ApiRouten.Kategorien.Liste, Dto("Lang", euerZeile: new string('x', 101))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client.PostAsJsonAsync(ApiRouten.Kategorien.Liste, Dto("Regex", matching: new MatchingDto(MatchingAlgorithmus.Regex, "(", false))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Doppelter_Name_in_anderer_Schreibweise_ergibt_409()
    {
        await AnlegenAsync(Dto("Büro"));

        (await Client.PostAsJsonAsync(ApiRouten.Kategorien.Liste, Dto("BÜRO")))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Zaehler_und_Jahressumme_ohne_Dubletten_und_andere_Jahre()
    {
        var kategorie = await AnlegenAsync(Dto("Reise"));
        var original = await fabrik.SpeichernAsync(Testdaten.Beleg(brutto: 100.00m, kategorieId: kategorie.Id, rechnungsdatum: new DateOnly(2026, 3, 1)));
        await fabrik.SpeichernAsync(Testdaten.Beleg(brutto: 50.50m, kategorieId: kategorie.Id, rechnungsdatum: new DateOnly(2026, 12, 31)));
        var dublette = Testdaten.Beleg(brutto: 100.00m, kategorieId: kategorie.Id, rechnungsdatum: new DateOnly(2026, 3, 1));
        dublette.DuplikatVonId = original.Id;
        await fabrik.SpeichernAsync(dublette);
        await fabrik.SpeichernAsync(Testdaten.Beleg(brutto: 999.00m, kategorieId: kategorie.Id, rechnungsdatum: new DateOnly(2025, 12, 31)));

        var jahr2026 = (await ListeAsync()).ShouldHaveSingleItem();
        var jahr2025 = (await ListeAsync(2025)).ShouldHaveSingleItem();

        jahr2026.AnzahlBelege.ShouldBe(2);
        jahr2026.SummeBrutto.ShouldBe(150.50m);
        jahr2025.AnzahlBelege.ShouldBe(1);
        jahr2025.SummeBrutto.ShouldBe(999.00m);
    }

    [Fact]
    public async Task Loeschen_setzt_Belege_und_Default_Kategorien_auf_leer()
    {
        var kategorie = await AnlegenAsync(Dto("Telefon"));
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(kategorieId: kategorie.Id));
        var korrespondent = Testdaten.Korrespondent();
        korrespondent.DefaultKategorieId = kategorie.Id;
        await fabrik.SpeichernAsync(korrespondent);

        var antwort = await Client.DeleteAsync(Einzeln(kategorie.Id));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.ReadFromJsonAsync<KategorieGeloeschtDto>()).ShouldBe(new KategorieGeloeschtDto(1, 1));
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Belege.SingleAsync(b => b.Id == beleg.Id)).KategorieId.ShouldBeNull();
        (await db.Korrespondenten.SingleAsync(k => k.Id == korrespondent.Id)).DefaultKategorieId.ShouldBeNull();
    }

    [Fact]
    public async Task Unbekannte_Kategorie_ergibt_404()
    {
        (await Client.PutAsJsonAsync(Einzeln(Guid.NewGuid()), Dto("X"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client.DeleteAsync(Einzeln(Guid.NewGuid()))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NurLesen_darf_lesen_aber_nicht_aendern()
    {
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await client.GetAsync(ApiRouten.Kategorien.Liste)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync(ApiRouten.Kategorien.Liste, Dto("Verboten"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync(Einzeln(Guid.NewGuid()))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
