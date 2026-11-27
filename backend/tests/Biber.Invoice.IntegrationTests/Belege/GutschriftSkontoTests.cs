using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.GutschriftBezug;
using Biber.Invoice.Infrastructure.Module.Skonto;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Belege;

[Collection("Api")]
public sealed class GutschriftSkontoTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    private Korrespondent _korrespondent = null!;

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(Jetzt);
        await fabrik.ZuruecksetzenAsync();
        foreach (var rolle in Enum.GetValues<Rolle>())
        {
            var benutzer = Testdaten.Benutzer();
            benutzer.Id = ApiFabrik.BenutzerId(rolle);
            await fabrik.SpeichernAsync(benutzer);
        }

        _korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Bürowelt Krämer KG"));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- Gutschrift ↔ Rechnung ----------

    [Fact]
    public async Task Gutschrift_wird_mit_Rechnung_verknuepft_und_die_Rechnung_zeigt_den_Restbetrag()
    {
        var rechnung = await BelegAsync(b => { b.Rechnungsnummer = "RE-0815"; b.Brutto = 264m; });
        var gutschrift = await BelegAsync(b => { b.Belegart = Belegart.Gutschrift; b.Rechnungsnummer = "GS-1"; b.Brutto = -64m; });

        var vorher = await DetailAsync(gutschrift.Id);
        vorher.Bezug.Kandidaten.Select(k => k.Id).ShouldBe([rechnung.Id]);

        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer)
            .PostJsonAsync(Route(ApiRouten.Belege.Bezug, gutschrift.Id), new BezugSetzenDto(rechnung.Id));

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await DetailAsync(gutschrift.Id)).Bezug.Rechnung!.Rechnungsnummer.ShouldBe("RE-0815");
        var detailRechnung = await DetailAsync(rechnung.Id);
        detailRechnung.Bezug.Gutschriften.Select(g => g.Rechnungsnummer).ShouldBe(["GS-1"]);
        detailRechnung.Bezug.Restbetrag.ShouldBe(200m);
    }

    [Fact]
    public async Task Bezug_nur_von_Gutschrift_auf_Rechnung_desselben_Korrespondenten()
    {
        var rechnung = await BelegAsync();
        var andere = await BelegAsync(b => b.KorrespondentId = null);
        var gutschrift = await BelegAsync(b => b.Belegart = Belegart.Gutschrift);
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        (await client.PostJsonAsync(Route(ApiRouten.Belege.Bezug, rechnung.Id), new BezugSetzenDto(gutschrift.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostJsonAsync(Route(ApiRouten.Belege.Bezug, gutschrift.Id), new BezugSetzenDto(andere.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostJsonAsync(Route(ApiRouten.Belege.Bezug, gutschrift.Id), new BezugSetzenDto(Guid.NewGuid())))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await fabrik.ErzeugeClient(Rolle.NurLesen).PostJsonAsync(Route(ApiRouten.Belege.Bezug, gutschrift.Id), new BezugSetzenDto(rechnung.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Pipeline_Schritt_findet_die_Rechnung_ueber_die_Nummer_im_Volltext()
    {
        var rechnung = await BelegAsync(b => b.Rechnungsnummer = "RE-2026-0815");
        await BelegAsync(b => b.Rechnungsnummer = "RE-2026-0816");
        var gutschrift = await BelegAsync(b =>
        {
            b.Belegart = Belegart.Gutschrift;
            b.Volltext = "Gutschrift zu Ihrer Rechnung RE-2026-0815 vom 03.11.2026";
        });

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var beleg = await db.Belege.SingleAsync(b => b.Id == gutschrift.Id);
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<GutschriftBezugSchritt>().Single();

        await schritt.AusfuehrenAsync(new VerarbeitungsKontext { Vorgang = new ImportVorgang(), Beleg = beleg, DateiPfad = "", Dateiname = "" }, CancellationToken.None);

        beleg.BezugBelegId.ShouldBe(rechnung.Id);
    }

    [Fact]
    public async Task Wechsel_der_Belegart_loest_den_Bezug()
    {
        var rechnung = await BelegAsync();
        var gutschrift = await BelegAsync(b => b.Belegart = Belegart.Gutschrift);
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);
        await client.PostJsonAsync(Route(ApiRouten.Belege.Bezug, gutschrift.Id), new BezugSetzenDto(rechnung.Id));
        var g = await LadenAsync(gutschrift.Id);

        var antwort = await client.PutJsonAsync(Route(ApiRouten.Belege.Einzeln, g.Id), Aenderung(g) with { Belegart = Belegart.Rechnung });

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LadenAsync(g.Id)).BezugBelegId.ShouldBeNull();
    }

    // ---------- Skonto ----------

    [Fact]
    public async Task Skonto_setzen_rechnet_den_Betrag_und_entfernen_leert_alles()
    {
        var rechnung = await BelegAsync(b => b.Brutto = 264m);
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PutJsonAsync(Route(ApiRouten.Belege.Skonto, rechnung.Id), new SkontoSetzenDto(2m, new DateOnly(2026, 11, 20)));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await antwort.Content.LesenAsync<BelegDetailDto>())!;
        detail.Skonto.ShouldBe(new SkontoDto(2m, new DateOnly(2026, 11, 20), 5.28m, Abgelaufen: true));

        antwort = await client.PutJsonAsync(Route(ApiRouten.Belege.Skonto, rechnung.Id), new SkontoSetzenDto(null, new DateOnly(2026, 12, 1)));

        (await antwort.Content.LesenAsync<BelegDetailDto>())!.Skonto.ShouldBeNull();
        var gespeichert = await LadenAsync(rechnung.Id);
        gespeichert.SkontoBis.ShouldBeNull();
        gespeichert.SkontoBetrag.ShouldBeNull();
    }

    [Fact]
    public async Task Skonto_nur_auf_Rechnungen_und_mit_gueltigem_Prozentsatz()
    {
        var gutschrift = await BelegAsync(b => b.Belegart = Belegart.Gutschrift);
        var rechnung = await BelegAsync();
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        (await client.PutJsonAsync(Route(ApiRouten.Belege.Skonto, gutschrift.Id), new SkontoSetzenDto(2m, null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PutJsonAsync(Route(ApiRouten.Belege.Skonto, rechnung.Id), new SkontoSetzenDto(0m, null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PutJsonAsync(Route(ApiRouten.Belege.Skonto, Guid.NewGuid()), new SkontoSetzenDto(2m, null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Skonto_Hinweis_meldet_offene_Rechnungen_vor_Fristende_genau_einmal()
    {
        var bald = await BelegAsync(b => { b.Rechnungsnummer = "RE-BALD"; b.SkontoProzent = 2m; b.SkontoBis = new DateOnly(2026, 11, 28); b.SkontoBetrag = 2.38m; });
        await BelegAsync(b => { b.SkontoProzent = 2m; b.SkontoBis = new DateOnly(2026, 12, 20); });
        await BelegAsync(b => { b.SkontoProzent = 2m; b.SkontoBis = new DateOnly(2026, 11, 25); });

        await JobAsync();
        await JobAsync();

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var meldungen = await db.Benachrichtigungen.Where(m => m.Art == BenachrichtigungArt.SkontoLaeuftAb).ToListAsync();
        meldungen.Count.ShouldBe(1);
        meldungen[0].Link.ShouldBe($"/belege/{bald.Id}");
        meldungen[0].Text.ShouldBe("Rechnung RE-BALD von Bürowelt Krämer KG: 2 % Skonto (spart 2,38 EUR) nur noch bis 28.11.2026.");
    }

    [Fact]
    public async Task Liste_traegt_das_Skonto_mit_Frist_und_Ablauf()
    {
        var laufend = await BelegAsync(b => { b.SkontoProzent = 2m; b.SkontoBis = new DateOnly(2026, 11, 26); b.SkontoBetrag = 2.38m; });
        var abgelaufen = await BelegAsync(b => { b.SkontoProzent = 3m; b.SkontoBis = new DateOnly(2026, 11, 25); });
        var ohne = await BelegAsync();

        var liste = (await fabrik.ErzeugeClient(Rolle.Benutzer).GetJsonAsync<BelegListeDto>(ApiRouten.Belege.Liste))!;

        var skonto = liste.Eintraege.ToDictionary(e => e.Id, e => e.Skonto);
        skonto[laufend.Id].ShouldBe(new SkontoDto(2m, new DateOnly(2026, 11, 26), 2.38m, Abgelaufen: false));
        skonto[abgelaufen.Id].ShouldBe(new SkontoDto(3m, new DateOnly(2026, 11, 25), null, Abgelaufen: true));
        skonto[ohne.Id].ShouldBeNull();
    }

    private async Task JobAsync()
    {
        using var scope = fabrik.ErzeugeScope();
        var job = scope.ServiceProvider.GetServices<IWiederkehrenderJob>().OfType<SkontoHinweisJob>().Single();
        await job.AusfuehrenAsync(CancellationToken.None);
    }

    private async Task<Beleg> BelegAsync(Action<Beleg>? anpassen = null)
    {
        var beleg = Testdaten.Beleg(korrespondentId: _korrespondent.Id);
        anpassen?.Invoke(beleg);
        return await fabrik.SpeichernAsync(beleg);
    }

    private async Task<BelegDetailDto> DetailAsync(Guid id) =>
        (await fabrik.ErzeugeClient(Rolle.Benutzer).GetJsonAsync<BelegDetailDto>(Route(ApiRouten.Belege.Einzeln, id)))!;

    private async Task<Beleg> LadenAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Belege.AsNoTracking().Include(b => b.Steuerzeilen).SingleAsync(b => b.Id == id);
    }

    private static BelegAendernDto Aenderung(Beleg b) => new(
        b.KorrespondentId,
        b.Belegart,
        b.Rechnungsnummer,
        b.Titel ?? "",
        b.Rechnungsdatum,
        b.Leistungsdatum,
        b.LeistungVon,
        b.LeistungBis,
        b.Faelligkeitsdatum,
        b.KategorieId,
        b.Waehrung,
        b.Netto,
        b.Steuer,
        b.Brutto,
        [.. b.Steuerzeilen.Select(s => new SteuerzeileAendernDto(s.Satz, s.Netto, s.Steuer))]);

    private static string Route(string vorlage, Guid id) => vorlage.Replace("{id}", id.ToString(), StringComparison.Ordinal);
}
