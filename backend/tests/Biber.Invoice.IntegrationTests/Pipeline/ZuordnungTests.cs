using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Zuordnung;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class ZuordnungTests(ApiFabrik fabrik) : IAsyncLifetime
{
    public Task InitializeAsync() => fabrik.ZuruecksetzenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Matching Regel(MatchingAlgorithmus algorithmus, string muster) =>
        new() { Algorithmus = algorithmus, Muster = muster };

    [Fact]
    public async Task Default_Kategorie_schlaegt_Regel()
    {
        var standard = await fabrik.SpeichernAsync(Testdaten.Kategorie("Zeta"));
        var nachRegel = Testdaten.Kategorie("Alpha");
        nachRegel.Matching = Regel(MatchingAlgorithmus.EinWort, "Hosting");
        await fabrik.SpeichernAsync(nachRegel);
        var korrespondent = Testdaten.Korrespondent();
        korrespondent.DefaultKategorieId = standard.Id;
        await fabrik.SpeichernAsync(korrespondent);

        var beleg = await ZuordnenAsync(Testdaten.Beleg(korrespondentId: korrespondent.Id, volltext: "Rechnung Hosting März"));

        beleg.KategorieId.ShouldBe(standard.Id);
    }

    [Fact]
    public async Task Regel_greift_ohne_Default_erste_Kategorie_nach_Name()
    {
        var b = Testdaten.Kategorie("Beta");
        b.Matching = Regel(MatchingAlgorithmus.Exakt, "Server");
        var a = Testdaten.Kategorie("Alpha");
        a.Matching = Regel(MatchingAlgorithmus.Regex, @"serv\w+");
        await fabrik.SpeichernAsync(b);
        await fabrik.SpeichernAsync(a);
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());

        var beleg = await ZuordnenAsync(Testdaten.Beleg(korrespondentId: korrespondent.Id, volltext: "Miete Server Q1"));

        beleg.KategorieId.ShouldBe(a.Id);
    }

    [Fact]
    public async Task Manuell_gesetzte_Kategorie_bleibt()
    {
        var manuell = await fabrik.SpeichernAsync(Testdaten.Kategorie("Manuell"));
        var regel = Testdaten.Kategorie("Regel");
        regel.Matching = Regel(MatchingAlgorithmus.EinWort, "Hosting");
        await fabrik.SpeichernAsync(regel);
        var vorlage = Testdaten.Beleg(kategorieId: manuell.Id, volltext: "Hosting");
        vorlage.KategorieManuell = true;

        var beleg = await ZuordnenAsync(vorlage);

        beleg.KategorieId.ShouldBe(manuell.Id);
    }

    [Fact]
    public async Task Mehrere_Tags_gleichzeitig_und_ohne_Setzer()
    {
        var t1 = Testdaten.Tag("Software");
        t1.Matching = Regel(MatchingAlgorithmus.EinWort, "Lizenz");
        var t2 = Testdaten.Tag("Abo");
        t2.Matching = Regel(MatchingAlgorithmus.AlleWoerter, "monatlich Abo");
        var t3 = Testdaten.Tag("Reise");
        t3.Matching = Regel(MatchingAlgorithmus.EinWort, "Bahn");
        await fabrik.SpeichernAsync(t1);
        await fabrik.SpeichernAsync(t2);
        await fabrik.SpeichernAsync(t3);

        var beleg = await ZuordnenAsync(Testdaten.Beleg(volltext: "Lizenz im Abo, monatlich abgerechnet"));

        var tags = await TagsAsync(beleg.Id);
        tags.Select(t => t.TagId).ShouldBe([t1.Id, t2.Id], ignoreOrder: true);
        tags.ShouldAllBe(t => t.GesetztVonId == null);
    }

    [Fact]
    public async Task System_Tag_wird_nie_per_Regel_gesetzt()
    {
        var system = Testdaten.Tag("zu prüfen");
        system.IstSystem = true;
        system.Matching = Regel(MatchingAlgorithmus.EinWort, "Rechnung");
        await fabrik.SpeichernAsync(system);

        var beleg = await ZuordnenAsync(Testdaten.Beleg(volltext: "Rechnung"));

        (await TagsAsync(beleg.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Auto_Regeln_werden_ignoriert()
    {
        var tag = Testdaten.Tag();
        tag.Matching = Regel(MatchingAlgorithmus.Auto, "Rechnung");
        await fabrik.SpeichernAsync(tag);
        var kategorie = Testdaten.Kategorie();
        kategorie.Matching = Regel(MatchingAlgorithmus.Auto, "Rechnung");
        await fabrik.SpeichernAsync(kategorie);

        var beleg = await ZuordnenAsync(Testdaten.Beleg(volltext: "Rechnung"));

        beleg.KategorieId.ShouldBeNull();
        (await TagsAsync(beleg.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Zweiter_Lauf_verdoppelt_keine_Tags()
    {
        var tag = Testdaten.Tag();
        tag.Matching = Regel(MatchingAlgorithmus.EinWort, "Strom");
        await fabrik.SpeichernAsync(tag);
        var beleg = await ZuordnenAsync(Testdaten.Beleg(volltext: "Strom Abschlag"));

        await ZuordnenAsync(beleg, speichern: false);

        (await TagsAsync(beleg.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Ungueltiger_Regex_bricht_die_Pipeline_nicht_ab()
    {
        var tag = Testdaten.Tag();
        tag.Matching = Regel(MatchingAlgorithmus.Regex, "([unvollständig");
        await fabrik.SpeichernAsync(tag);
        var gueltig = Testdaten.Tag();
        gueltig.Matching = Regel(MatchingAlgorithmus.EinWort, "Strom");
        await fabrik.SpeichernAsync(gueltig);
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Strom ([unvollständig"));

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<ZuordnungSchritt>().Single();
        var geladen = await db.Belege.Include(b => b.Tags).SingleAsync(b => b.Id == beleg.Id);
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = new ImportVorgang { Id = Guid.NewGuid() },
            Beleg = geladen,
            DateiPfad = "x.pdf",
            Dateiname = "x.pdf",
        };

        var ergebnis = await schritt.AusfuehrenAsync(kontext, CancellationToken.None);
        await db.SaveChangesAsync();

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        schritt.Name.ShouldBe("Zuordnung");
        schritt.Reihenfolge.ShouldBe(SchrittReihenfolge.Zuordnung);
        (await TagsAsync(beleg.Id)).Select(t => t.TagId).ShouldBe([gueltig.Id]);
    }

    /// <summary>Speichert den Beleg (optional), lädt ihn in einem frischen Scope, ordnet zu und speichert.</summary>
    private async Task<Beleg> ZuordnenAsync(Beleg beleg, bool speichern = true)
    {
        if (speichern)
        {
            await fabrik.SpeichernAsync(beleg);
        }

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var dienst = scope.ServiceProvider.GetRequiredService<IZuordnungsDienst>();
        var geladen = await db.Belege.SingleAsync(b => b.Id == beleg.Id);
        await dienst.ZuordnenAsync(geladen, CancellationToken.None);
        await db.SaveChangesAsync();
        return geladen;
    }

    private async Task<List<BelegTag>> TagsAsync(Guid belegId)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.BelegTags.AsNoTracking().Where(t => t.BelegId == belegId).ToListAsync();
    }
}
