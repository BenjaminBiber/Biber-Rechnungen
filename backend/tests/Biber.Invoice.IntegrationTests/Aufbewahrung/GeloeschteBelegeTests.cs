using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Aufbewahrung;

[Collection("Api")]
public sealed class GeloeschteBelegeTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly BelegLoeschenDto FalschZugeordnet = new(LoeschGrund.FalschZugeordnet, "Rechnung vom Nachbarn");

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
        foreach (var rolle in Enum.GetValues<Rolle>())
        {
            var benutzer = Testdaten.Benutzer();
            benutzer.Id = ApiFabrik.BenutzerId(rolle);
            await fabrik.SpeichernAsync(benutzer);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Loeschen_umgeht_die_Loeschsperre_blendet_den_Beleg_aus_und_verschiebt_das_Original()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Muster GmbH"));
        var beleg = Testdaten.Beleg("RE-1", new DateOnly(2026, 3, 1), 119m, korrespondent.Id);
        const string ablagePfad = "2026/Muster_GmbH/2026-03-01_RE-1.pdf";
        var original = DateiAnlegen(ablagePfad);
        var datei = Datei(beleg.Id, ablagePfad);
        beleg.Dateien.Add(datei);
        await fabrik.SpeichernAsync(beleg);
        var client = fabrik.ErzeugeClient();

        var antwort = await client.PostJsonAsync(Pfad(ApiRouten.Belege.Loeschen, beleg.Id), FalschZugeordnet);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync(Pfad(ApiRouten.Belege.Einzeln, beleg.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var liste = (await client.GetJsonAsync<BelegListeDto>(ApiRouten.Belege.Liste)).ShouldNotBeNull();
        liste.Eintraege.ShouldBeEmpty();

        File.Exists(original).ShouldBeFalse();
        File.Exists(Path.Combine(AblageOrdner, "_Geloescht", ablagePfad)).ShouldBeTrue();

        var geloescht = (await client.GetJsonAsync<GeloeschterBelegDto[]>(ApiRouten.Belege.Geloeschte))
            .ShouldNotBeNull().ShouldHaveSingleItem();
        geloescht.Id.ShouldBe(beleg.Id);
        geloescht.Rechnungsnummer.ShouldBe("RE-1");
        geloescht.Korrespondent.ShouldBe("Muster GmbH");
        geloescht.Grund.ShouldBe(LoeschGrund.FalschZugeordnet);
        geloescht.Bemerkung.ShouldBe("Rechnung vom Nachbarn");
        geloescht.GeloeschtAm.ShouldBe(fabrik.Zeit.GetUtcNow());
        geloescht.OriginalId.ShouldBe(datei.Id);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var gespeichert = await db.Belege.IgnoreQueryFilters().Include(b => b.Dateien).SingleAsync(b => b.Id == beleg.Id);
        gespeichert.GeloeschtVonId.ShouldBe(ApiFabrik.BenutzerId(Rolle.Admin));
        gespeichert.Dateien.ShouldHaveSingleItem().Pfad.ShouldBe($"_Geloescht/{ablagePfad}");
        (await db.AuditEintraege.AnyAsync(a => a.BelegId == beleg.Id && a.Aktion == "FalschZugeordnetGeloescht"))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Wiederherstellen_zeigt_den_Beleg_wieder_an_und_legt_das_Original_zurueck()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Muster GmbH"));
        var beleg = Testdaten.Beleg("RE-2", new DateOnly(2026, 3, 1), 119m, korrespondent.Id);
        const string ablagePfad = "2026/Muster_GmbH/2026-03-01_RE-2.pdf";
        var original = DateiAnlegen(ablagePfad);
        beleg.Dateien.Add(Datei(beleg.Id, ablagePfad));
        await fabrik.SpeichernAsync(beleg);
        var client = fabrik.ErzeugeClient();
        (await client.PostJsonAsync(Pfad(ApiRouten.Belege.Loeschen, beleg.Id), FalschZugeordnet))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var antwort = await client.PostAsync(Pfad(ApiRouten.Belege.Wiederherstellen, beleg.Id), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync(Pfad(ApiRouten.Belege.Einzeln, beleg.Id))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetJsonAsync<GeloeschterBelegDto[]>(ApiRouten.Belege.Geloeschte)).ShouldNotBeNull().ShouldBeEmpty();
        File.Exists(original).ShouldBeTrue();
        File.Exists(Path.Combine(AblageOrdner, "_Geloescht", ablagePfad)).ShouldBeFalse();

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var gespeichert = await db.Belege.Include(b => b.Dateien).SingleAsync(b => b.Id == beleg.Id);
        gespeichert.GeloeschtAm.ShouldBeNull();
        gespeichert.GeloeschtVonId.ShouldBeNull();
        gespeichert.LoeschGrund.ShouldBeNull();
        gespeichert.LoeschBemerkung.ShouldBeNull();
        gespeichert.Dateien.ShouldHaveSingleItem().Pfad.ShouldBe(ablagePfad);
    }

    [Fact]
    public async Task Ohne_Grund_liefert_400()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());

        var antwort = await fabrik.ErzeugeClient()
            .PostJsonAsync(Pfad(ApiRouten.Belege.Loeschen, beleg.Id), new BelegLoeschenDto(null, "versehentlich"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await GeloeschtAsync(beleg.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Beleg_in_Verarbeitung_liefert_409()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(status: BelegStatus.InVerarbeitung));

        var antwort = await fabrik.ErzeugeClient().PostJsonAsync(Pfad(ApiRouten.Belege.Loeschen, beleg.Id), FalschZugeordnet);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await GeloeschtAsync(beleg.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Unbekannter_oder_nicht_geloeschter_Beleg_liefert_404()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());
        var client = fabrik.ErzeugeClient();

        (await client.PostJsonAsync(Pfad(ApiRouten.Belege.Loeschen, Guid.NewGuid()), FalschZugeordnet))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsync(Pfad(ApiRouten.Belege.Wiederherstellen, beleg.Id), null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(Rolle.Benutzer)]
    [InlineData(Rolle.NurLesen)]
    public async Task Ohne_Admin_Rolle_liefern_alle_Endpunkte_403(Rolle rolle)
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());
        var client = fabrik.ErzeugeClient(rolle);

        (await client.PostJsonAsync(Pfad(ApiRouten.Belege.Loeschen, beleg.Id), FalschZugeordnet))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync(ApiRouten.Belege.Geloeschte)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync(Pfad(ApiRouten.Belege.Wiederherstellen, beleg.Id), null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GeloeschtAsync(beleg.Id)).ShouldBeFalse();
    }

    private string AblageOrdner => Path.Combine(fabrik.TempOrdner, "ablage");

    private static string Pfad(string route, Guid id) => route.Replace("{id}", id.ToString(), StringComparison.Ordinal);

    private async Task<bool> GeloeschtAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Belege.IgnoreQueryFilters()
            .AnyAsync(b => b.Id == id && b.GeloeschtAm != null);
    }

    private string DateiAnlegen(string relativ)
    {
        var absolut = Path.Combine(AblageOrdner, relativ);
        Directory.CreateDirectory(Path.GetDirectoryName(absolut)!);
        File.WriteAllText(absolut, "inhalt");
        return absolut;
    }

    private static BelegDatei Datei(Guid belegId, string pfad) => new()
    {
        Id = Guid.NewGuid(),
        BelegId = belegId,
        Rolle = DateiRolle.Original,
        Pfad = pfad,
        Sha256 = "00",
        MimeType = "application/pdf",
        AbgelegtAm = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    };
}
