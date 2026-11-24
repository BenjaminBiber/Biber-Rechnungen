using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.AutoMatching;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.AutoMatching;

/// <summary>
/// Synthetischer, klar trennbarer Bestand: Hosting-Texte → „Software &amp; Hosting“ / Hoster, Papier-Texte →
/// „Bürobedarf“ / Bürohändler, jeder zweite Text mit Lastschrift-Satz → Tag „Lastschrift“. Alle IDs sind fest,
/// damit Aufteilung und Training (fester Seed) reproduzierbar sind.
/// </summary>
[Collection("Api")]
public sealed class AutoMatchingTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private const string Lastschrift = "Der Betrag wird per Lastschrift eingezogen.";
    private const string Ueberweisung = "Bitte überweisen Sie den Betrag innerhalb von 14 Tagen.";

    private static readonly DateTimeOffset Zeitpunkt = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid HostingId = FesteId(1, 1);
    private static readonly Guid BueroId = FesteId(1, 2);
    private static readonly Guid ReiseId = FesteId(1, 3);
    private static readonly Guid HosterId = FesteId(2, 1);
    private static readonly Guid BueroHaendlerId = FesteId(2, 2);
    private static readonly Guid LastschriftId = FesteId(3, 1);

    public Task InitializeAsync() => fabrik.ZuruecksetzenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Guid FesteId(int art, int nummer) => new($"{art:D8}-0000-0000-0000-{nummer:D12}");

    private static string HostingText(int i, bool lastschrift = false) =>
        $"Rechnung Nr. {1000 + i} Hosting Server Domain Vertrag Rechenzentrum Monat {(i % 12) + 1}. {(lastschrift ? Lastschrift : Ueberweisung)}";

    private static string PapierText(int i, bool lastschrift = false) =>
        $"Rechnung Nr. {5000 + i} Papier Toner Ordner Kopierpapier Lieferung Filiale {(i % 7) + 1}. {(lastschrift ? Lastschrift : Ueberweisung)}";

    /// <summary>Stammdaten: zwei Auto-Kategorien, zwei Auto-Korrespondenten, Auto-Tag „Lastschrift“.</summary>
    private async Task StammdatenAsync(MatchingAlgorithmus hosting = MatchingAlgorithmus.Auto)
    {
        await fabrik.SpeichernAsync(Kategorie(HostingId, "Software & Hosting", hosting));
        await fabrik.SpeichernAsync(Kategorie(BueroId, "Bürobedarf", MatchingAlgorithmus.Auto));
        await fabrik.SpeichernAsync(Korrespondent(HosterId, "Hoster GmbH"));
        await fabrik.SpeichernAsync(Korrespondent(BueroHaendlerId, "Büro Handel KG"));
        var tag = Testdaten.Tag("Lastschrift");
        tag.Id = LastschriftId;
        tag.Matching = new Matching { Algorithmus = MatchingAlgorithmus.Auto };
        await fabrik.SpeichernAsync(tag);
    }

    private static Kategorie Kategorie(Guid id, string name, MatchingAlgorithmus algorithmus)
    {
        var kategorie = Testdaten.Kategorie(name);
        kategorie.Id = id;
        kategorie.Matching = new Matching { Algorithmus = algorithmus };
        return kategorie;
    }

    private static Korrespondent Korrespondent(Guid id, string name)
    {
        var korrespondent = Testdaten.Korrespondent(name);
        korrespondent.Id = id;
        korrespondent.Matching = new Matching { Algorithmus = MatchingAlgorithmus.Auto };
        return korrespondent;
    }

    /// <summary>Je <paramref name="jeKlasse"/> Hosting- und Papier-Belege; jeder zweite mit Tag „Lastschrift“.</summary>
    private async Task BestandAsync(int jeKlasse = 40)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        for (var i = 0; i < jeKlasse; i++)
        {
            var lastschrift = i % 2 == 0;
            db.Belege.Add(TrainingsBeleg(FesteId(10, i), HostingText(i, lastschrift), HostingId, HosterId, lastschrift));
            db.Belege.Add(TrainingsBeleg(FesteId(11, i), PapierText(i, lastschrift), BueroId, BueroHaendlerId, lastschrift));
        }

        await db.SaveChangesAsync();
    }

    private static Beleg TrainingsBeleg(Guid id, string text, Guid? kategorieId, Guid? korrespondentId, bool lastschrift)
    {
        var beleg = Testdaten.Beleg(korrespondentId: korrespondentId, kategorieId: kategorieId, volltext: text);
        beleg.Id = id;
        if (lastschrift)
        {
            beleg.Tags.Add(new BelegTag { BelegId = id, TagId = LastschriftId, GesetztAm = Zeitpunkt });
        }

        return beleg;
    }

    private async Task TrainierenAsync()
    {
        using var scope = fabrik.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<AutoMatchingTrainer>().TrainierenAsync(CancellationToken.None);
    }

    private async Task<List<MatchingModell>> ModelleAsync()
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.MatchingModelle.AsNoTracking().OrderBy(m => m.Ziel).ToListAsync();
    }

    /// <summary>Speichert <paramref name="beleg"/>, führt den Schritt in einem frischen Scope aus und speichert.</summary>
    private async Task<(SchrittErgebnis Ergebnis, Beleg Beleg, List<Guid> Tags)> SchrittAsync(Beleg beleg)
    {
        await fabrik.SpeichernAsync(beleg);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<AutoZuordnungSchritt>().Single();
        var geladen = await db.Belege.Include(b => b.Tags).SingleAsync(b => b.Id == beleg.Id);
        var ergebnis = await schritt.AusfuehrenAsync(
            new VerarbeitungsKontext
            {
                Vorgang = new ImportVorgang { Id = Guid.NewGuid() },
                Beleg = geladen,
                DateiPfad = "neu.pdf",
                Dateiname = "neu.pdf",
            },
            CancellationToken.None);
        await db.SaveChangesAsync();
        var tags = await db.BelegTags.AsNoTracking().Where(t => t.BelegId == beleg.Id).Select(t => t.TagId).ToListAsync();
        return (ergebnis, geladen, tags);
    }

    private static Beleg NeuerBeleg(string text) => Testdaten.Beleg(volltext: text, status: BelegStatus.InVerarbeitung);

    [Fact]
    public async Task Training_erzeugt_je_Ziel_ein_Modell_mit_Anzahl_und_Guete()
    {
        await StammdatenAsync();
        await BestandAsync();

        await TrainierenAsync();

        var modelle = await ModelleAsync();
        modelle.Select(m => m.Ziel).ShouldBe(["Kategorie", "Korrespondent", "Tag"]);
        modelle.ShouldAllBe(m => m.AnzahlBeispiele == 80 && m.Guete >= 0.9 && m.Daten.Length > 0);
        modelle.ShouldAllBe(m => m.TrainiertAm == fabrik.Zeit.GetUtcNow());
    }

    [Fact]
    public async Task Neuer_Hosting_Beleg_bekommt_Kategorie_Korrespondent_und_Tag()
    {
        await StammdatenAsync();
        await BestandAsync();
        await TrainierenAsync();

        var (ergebnis, beleg, tags) = await SchrittAsync(NeuerBeleg(HostingText(77, lastschrift: true)));

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        beleg.KategorieId.ShouldBe(HostingId);
        beleg.KorrespondentId.ShouldBe(HosterId);
        tags.ShouldBe([LastschriftId]);
    }

    [Fact]
    public async Task Papier_Beleg_ohne_Lastschrift_bekommt_keinen_Tag()
    {
        await StammdatenAsync();
        await BestandAsync();
        await TrainierenAsync();

        var (_, beleg, tags) = await SchrittAsync(NeuerBeleg(PapierText(77)));

        beleg.KategorieId.ShouldBe(BueroId);
        tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unter_50_Trainingsbelegen_entsteht_kein_Modell()
    {
        await StammdatenAsync();
        await BestandAsync(jeKlasse: 24); // 48 bestätigte Belege

        // Unbestätigt, Dublette und nicht fertig zählen nicht mit.
        var unbestaetigt = Testdaten.Beleg(kategorieId: HostingId, volltext: HostingText(90));
        unbestaetigt.Tags.Add(new BelegTag { BelegId = unbestaetigt.Id, TagId = SystemIds.TagZuPruefen, GesetztAm = Zeitpunkt });
        await fabrik.SpeichernAsync(unbestaetigt);
        var dublette = Testdaten.Beleg(kategorieId: HostingId, volltext: HostingText(91));
        dublette.DuplikatVonId = FesteId(10, 0);
        await fabrik.SpeichernAsync(dublette);
        await fabrik.SpeichernAsync(Testdaten.Beleg(kategorieId: HostingId, volltext: HostingText(92), status: BelegStatus.InVerarbeitung));

        await TrainierenAsync();

        (await ModelleAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Ziele_unter_fuenf_Beispielen_und_ohne_Auto_werden_nicht_gelernt()
    {
        await StammdatenAsync();
        await BestandAsync();
        var reise = Kategorie(ReiseId, "Reisekosten", MatchingAlgorithmus.Keiner);
        await fabrik.SpeichernAsync(reise);
        for (var i = 0; i < 20; i++)
        {
            await fabrik.SpeichernAsync(Testdaten.Beleg(kategorieId: ReiseId, volltext: $"Fahrkarte Bahn ICE Reise Ticket {i}"));
        }

        // Tag „Lastschrift“ nur viermal: zu wenig für ein Tag-Modell.
        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            db.BelegTags.RemoveRange(db.BelegTags.Where(t => t.TagId == LastschriftId).OrderBy(t => t.BelegId).Skip(4));
            await db.SaveChangesAsync();
        }

        await TrainierenAsync();

        (await ModelleAsync()).Select(m => m.Ziel).ShouldBe(["Kategorie", "Korrespondent"]);
        var (_, beleg, tags) = await SchrittAsync(NeuerBeleg("Fahrkarte Bahn ICE Reise Ticket 99"));
        beleg.KategorieId.ShouldBeNull();
        tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ziel_das_nach_dem_Training_nicht_mehr_Auto_ist_wird_nicht_gesetzt()
    {
        await StammdatenAsync();
        await BestandAsync();
        await TrainierenAsync();
        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            (await db.Kategorien.SingleAsync(k => k.Id == HostingId)).Matching.Algorithmus = MatchingAlgorithmus.Keiner;
            (await db.Tags.SingleAsync(t => t.Id == LastschriftId)).Matching.Algorithmus = MatchingAlgorithmus.EinWort;
            await db.SaveChangesAsync();
        }

        var (_, beleg, tags) = await SchrittAsync(NeuerBeleg(HostingText(77, lastschrift: true)));

        beleg.KategorieId.ShouldBeNull();
        beleg.KorrespondentId.ShouldBe(HosterId);
        tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Manuelle_und_regelbasierte_Zuweisungen_bleiben()
    {
        await StammdatenAsync();
        await BestandAsync();
        await TrainierenAsync();

        var manuell = NeuerBeleg(HostingText(77));
        manuell.KategorieId = BueroId;
        manuell.KategorieManuell = true;
        var (_, beleg, _) = await SchrittAsync(manuell);
        beleg.KategorieId.ShouldBe(BueroId);

        var manuellOhneKategorie = NeuerBeleg(HostingText(78));
        manuellOhneKategorie.KategorieManuell = true;
        (_, beleg, _) = await SchrittAsync(manuellOhneKategorie);
        beleg.KategorieId.ShouldBeNull();

        var perRegel = Testdaten.Beleg(korrespondentId: BueroHaendlerId, kategorieId: BueroId, volltext: HostingText(79), status: BelegStatus.InVerarbeitung);
        (_, beleg, _) = await SchrittAsync(perRegel);
        beleg.KategorieId.ShouldBe(BueroId);
        beleg.KorrespondentId.ShouldBe(BueroHaendlerId);
    }

    [Theory]
    [InlineData("Lorem ipsum dolor sit amet, consectetur adipiscing elit.")]
    [InlineData("Mietvertrag Wohnung")]
    [InlineData("Hosting Toner")]
    public async Task Unklarer_Text_unter_der_Schwelle_aendert_nichts(string text)
    {
        await StammdatenAsync();
        await BestandAsync();
        await TrainierenAsync();

        var (ergebnis, beleg, tags) = await SchrittAsync(NeuerBeleg(text));

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        beleg.KategorieId.ShouldBeNull();
        beleg.KorrespondentId.ShouldBeNull();
        tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Zweites_Training_ersetzt_die_Modelle()
    {
        await StammdatenAsync();
        await BestandAsync();
        await TrainierenAsync();
        var erste = await ModelleAsync();

        fabrik.Zeit.Vorspulen(TimeSpan.FromDays(1));
        await TrainierenAsync();

        var zweite = await ModelleAsync();
        zweite.Count.ShouldBe(3);
        zweite.ShouldAllBe(m => m.TrainiertAm == fabrik.Zeit.GetUtcNow());
        zweite.Select(m => m.Id).ShouldNotContain(id => erste.Select(e => e.Id).Contains(id));

        // Die Vorhersage lädt die neuen Modelle.
        var (_, beleg, _) = await SchrittAsync(NeuerBeleg(PapierText(77)));
        beleg.KategorieId.ShouldBe(BueroId);
    }

    [Fact]
    public async Task Status_liefert_Zeitpunkt_Anzahl_und_Guete()
    {
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);
        (await client.GetJsonAsync<AutoMatchingStatusDto[]>(ApiRouten.Matching.AutoStatus)).ShouldBe([]);

        await StammdatenAsync();
        await BestandAsync();
        await TrainierenAsync();

        var status = (await client.GetJsonAsync<AutoMatchingStatusDto[]>(ApiRouten.Matching.AutoStatus)).ShouldNotBeNull();
        status.Select(s => s.Ziel).ShouldBe(["Kategorie", "Korrespondent", "Tag"]);
        status.ShouldAllBe(s => s.TrainiertAm == fabrik.Zeit.GetUtcNow() && s.AnzahlBeispiele == 80 && s.Guete >= 0.9m);
        (await fabrik.ErzeugeAnonymenClient().GetAsync(ApiRouten.Matching.AutoStatus)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ohne_Modell_laeuft_der_Schritt_unveraendert_durch()
    {
        await StammdatenAsync();

        var (ergebnis, beleg, tags) = await SchrittAsync(NeuerBeleg(HostingText(1, lastschrift: true)));

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        beleg.KategorieId.ShouldBeNull();
        beleg.KorrespondentId.ShouldBeNull();
        tags.ShouldBeEmpty();
        using var scope = fabrik.ErzeugeScope();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<AutoZuordnungSchritt>().Single();
        schritt.Name.ShouldBe("Auto-Zuordnung");
        schritt.Reihenfolge.ShouldBe(SchrittReihenfolge.AutoZuordnung);
    }

    [Fact]
    public async Task Nachtjob_trainiert_um_halb_drei()
    {
        await StammdatenAsync();
        await BestandAsync();
        using var scope = fabrik.ErzeugeScope();
        var job = scope.ServiceProvider.GetServices<IWiederkehrenderJob>().OfType<AutoMatchingJob>().Single();

        await job.AusfuehrenAsync(CancellationToken.None);

        job.Id.ShouldBe("auto-matching");
        job.Cron.ShouldBe("30 2 * * *");
        (await ModelleAsync()).Count.ShouldBe(3);
    }
}
