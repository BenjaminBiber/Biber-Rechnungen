using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Aufbewahrung;

[Collection("Api")]
public sealed class AufbewahrungTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateTimeOffset LetzterSperrtag = new(2034, 12, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ErsterFreierTag = new(2035, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Beleg_aus_2026_ist_am_31_12_2034_gesperrt_und_am_01_01_2035_loeschbar()
    {
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 3, 1)));
        var client = fabrik.ErzeugeClient();

        fabrik.Zeit.Setze(LetzterSperrtag);
        (await LoeschbarAsync(client)).ShouldBeEmpty();

        fabrik.Zeit.Setze(ErsterFreierTag);
        var zeile = (await LoeschbarAsync(client)).ShouldHaveSingleItem();
        zeile.Jahr.ShouldBe(2026);
        zeile.Anzahl.ShouldBe(1);
        zeile.FristEnde.ShouldBe(new DateOnly(2034, 12, 31));
    }

    [Fact]
    public async Task Liste_ist_nach_Belegjahr_gruppiert()
    {
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2025, 2, 1)));
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 2, 1)));
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 9, 1)));
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2027, 1, 1)));
        fabrik.Zeit.Setze(ErsterFreierTag);

        var liste = await LoeschbarAsync(fabrik.ErzeugeClient());

        liste.Select(l => (l.Jahr, l.Anzahl)).ShouldBe([(2025, 1), (2026, 2)]);
    }

    [Fact]
    public async Task Geaenderte_Frist_verschiebt_das_Fristende()
    {
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 3, 1)));
        using (var scope = fabrik.ErzeugeScope())
        {
            await scope.ServiceProvider.GetRequiredService<IEinstellungen>()
                .SpeichernAsync(new AufbewahrungEinstellungen { Jahre = 10 }, CancellationToken.None);
        }

        var client = fabrik.ErzeugeClient();
        fabrik.Zeit.Setze(ErsterFreierTag);
        (await LoeschbarAsync(client)).ShouldBeEmpty();

        fabrik.Zeit.Setze(new DateTimeOffset(2037, 1, 1, 12, 0, 0, TimeSpan.Zero));
        (await LoeschbarAsync(client)).ShouldHaveSingleItem().FristEnde.ShouldBe(new DateOnly(2036, 12, 31));
    }

    [Fact]
    public async Task Falsche_Bestaetigung_liefert_400()
    {
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 3, 1)));
        fabrik.Zeit.Setze(ErsterFreierTag);

        var antwort = await fabrik.ErzeugeClient()
            .PostJsonAsync(ApiRouten.Aufbewahrung.Loeschen, new LoeschenDto(2026, "löschen"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await AnzahlBelegeAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Zu_frueh_liefert_409()
    {
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 3, 1)));
        fabrik.Zeit.Setze(LetzterSperrtag);

        var antwort = await fabrik.ErzeugeClient()
            .PostJsonAsync(ApiRouten.Aufbewahrung.Loeschen, new LoeschenDto(2026, "LÖSCHEN"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await AnzahlBelegeAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Loeschen_entfernt_App_Daten_und_laesst_die_Ablage_unberuehrt()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Muster GmbH"));
        var beleg = Testdaten.Beleg("RE-1", new DateOnly(2026, 3, 1), 119m, korrespondent.Id);
        beleg.Positionen.Add(new Position { Id = Guid.NewGuid(), BelegId = beleg.Id, Text = "Papier" });
        const string ablagePfad = "2026/Muster GmbH/2026-03-01_RE-1.pdf";
        var original = DateiAnlegen(Path.Combine(fabrik.TempOrdner, "ablage"), ablagePfad);
        var visualisierungPfad = Path.Combine("visualisierung", $"{beleg.Id}.html");
        var visualisierung = DateiAnlegen(Path.Combine(fabrik.TempOrdner, "daten"), visualisierungPfad);
        beleg.Dateien.Add(Datei(beleg.Id, DateiRolle.Original, ablagePfad));
        beleg.Dateien.Add(Datei(beleg.Id, DateiRolle.Visualisierung, visualisierungPfad));
        var vorgang = new ImportVorgang
        {
            Id = Guid.NewGuid(),
            Quelle = ImportQuelle.Upload,
            Dateiname = "a.pdf",
            BelegId = beleg.Id,
            ErstelltAm = fabrik.Zeit.GetUtcNow(),
        };
        await fabrik.SpeichernAsync(beleg);
        await fabrik.SpeichernAsync(vorgang);
        var alteEintraege = await AuditAsync();
        fabrik.Zeit.Setze(ErsterFreierTag);

        var antwort = await fabrik.ErzeugeClient()
            .PostJsonAsync(ApiRouten.Aufbewahrung.Loeschen, new LoeschenDto(2026, "LÖSCHEN"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ergebnis = (await antwort.Content.LesenAsync<LoeschErgebnisDto>()).ShouldNotBeNull();
        ergebnis.Geloescht.ShouldBe(1);
        ergebnis.Pfade.ShouldBe([ablagePfad]);
        File.Exists(original).ShouldBeTrue();
        File.Exists(visualisierung).ShouldBeFalse();

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Belege.AnyAsync()).ShouldBeFalse();
        (await db.Positionen.AnyAsync()).ShouldBeFalse();
        (await db.BelegDateien.AnyAsync()).ShouldBeFalse();
        (await db.ImportVorgaenge.SingleAsync()).BelegId.ShouldBeNull();

        var eintraege = await AuditAsync();
        foreach (var alt in alteEintraege)
        {
            var jetzt = eintraege.Single(e => e.Id == alt.Id);
            (jetzt.Aktion, jetzt.NeuerWert, jetzt.AlterWert).ShouldBe((alt.Aktion, alt.NeuerWert, alt.AlterWert));
        }

        var loeschung = eintraege
            .Where(e => e.Aktion == "Geloescht" && e.EntitaetId == beleg.Id.ToString() && e.NeuerWert != null)
            .ShouldHaveSingleItem().NeuerWert.ShouldNotBeNull();
        loeschung.ShouldContain("RE-1");
        loeschung.ShouldContain("Muster GmbH");
        loeschung.ShouldContain("2026-03-01");
        loeschung.ShouldContain("119.00");
        loeschung.ShouldContain(ablagePfad);
    }

    [Fact]
    public async Task Direktes_Loeschen_ueber_den_DbContext_wirft()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2000, 1, 1)));

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        db.Belege.Remove(await db.Belege.SingleAsync(b => b.Id == beleg.Id));

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        (await AnzahlBelegeAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Direktes_Loeschen_eines_Originals_wirft()
    {
        var beleg = Testdaten.Beleg();
        beleg.Dateien.Add(Datei(beleg.Id, DateiRolle.Original, "2026/x.pdf"));
        await fabrik.SpeichernAsync(beleg);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        db.BelegDateien.Remove(await db.BelegDateien.SingleAsync());

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(Rolle.Benutzer)]
    [InlineData(Rolle.NurLesen)]
    public async Task Ohne_Admin_Rolle_liefern_beide_Endpunkte_403(Rolle rolle)
    {
        var client = fabrik.ErzeugeClient(rolle);

        (await client.GetAsync(ApiRouten.Aufbewahrung.Loeschbar)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostJsonAsync(ApiRouten.Aufbewahrung.Loeschen, new LoeschenDto(2026, "LÖSCHEN")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<LoeschbarDto[]> LoeschbarAsync(HttpClient client) =>
        (await client.GetJsonAsync<LoeschbarDto[]>(ApiRouten.Aufbewahrung.Loeschbar)).ShouldNotBeNull();

    private async Task<int> AnzahlBelegeAsync()
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Belege.CountAsync();
    }

    private async Task<List<AuditEintrag>> AuditAsync()
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().AuditEintraege.AsNoTracking()
            .ToListAsync();
    }

    private static BelegDatei Datei(Guid belegId, DateiRolle rolle, string pfad) => new()
    {
        Id = Guid.NewGuid(),
        BelegId = belegId,
        Rolle = rolle,
        Pfad = pfad,
        Sha256 = "00",
        MimeType = "application/pdf",
        AbgelegtAm = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    };

    private static string DateiAnlegen(string wurzel, string relativ)
    {
        var absolut = Path.Combine(wurzel, relativ);
        Directory.CreateDirectory(Path.GetDirectoryName(absolut)!);
        File.WriteAllText(absolut, "inhalt");
        return absolut;
    }
}
