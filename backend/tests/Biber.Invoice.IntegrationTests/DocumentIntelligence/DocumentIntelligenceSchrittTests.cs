using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.DocumentIntelligence;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.DocumentIntelligence;

/// <summary>Schritt 3b in der echten Pipeline (Orchestrator, Datenbank, Benachrichtigungen); Azure als Fake.</summary>
[Collection("Api")]
public sealed class DocumentIntelligenceSchrittTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateOnly Januar = new(2026, 1, 1);
    private static readonly DateOnly Februar = new(2026, 2, 1);

    public async Task InitializeAsync()
    {
        await fabrik.ZuruecksetzenAsync();
        await fabrik.SystemTagsSicherstellenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task E_Rechnung_wird_uebersprungen()
    {
        var di = new FakeDiClient();
        var f = fabrik.MitDi(di, ErkanntesFormat.XRechnungXml);

        var vorgang = await f.ImportiereAsync("rechnung.xml", "<Invoice/>"u8.ToArray());

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        di.Aufrufe.ShouldBe(0);
        (await f.VerbrauchAsync(Januar)).ShouldBe(0);
        vorgang.Schritte.Select(s => s.Name).ShouldContain("Document Intelligence");
    }

    [Fact]
    public async Task Bild_wird_ausgelesen_und_der_Verbrauch_erhoeht()
    {
        var di = new FakeDiClient { Seiten = 2 };
        var f = fabrik.MitDi(di, ErkanntesFormat.Bild, seiten: 2);

        var vorgang = await f.ImportiereAsync("rechnung.png", [0x89, 0x50, 0x4E, 0x47]);

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        di.Aufrufe.ShouldBe(1);
        (await f.VerbrauchAsync(Januar)).ShouldBe(2);
        var beleg = await f.BelegAsync(vorgang.BelegId!.Value);
        beleg.ExtraktionsQuelle.ShouldBe(ExtraktionsQuelle.DocumentIntelligence);
        beleg.Rechnungsnummer.ShouldBe("RE-2026-0815");
        beleg.DiKonfidenz.ShouldBe(0.9m);
        beleg.Seitenanzahl.ShouldBe(2);
    }

    [Fact]
    public async Task Zu_viele_Seiten_werden_nicht_ausgelesen()
    {
        var di = new FakeDiClient();
        var f = fabrik.MitDi(di, seiten: 11);
        await f.DiEinstellenAsync(maxSeiten: 10);

        var vorgang = await f.ImportiereAsync("handbuch.pdf", DiTestHilfen.Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        di.Aufrufe.ShouldBe(0);
        (await f.VerbrauchAsync(Januar)).ShouldBe(0);
        (await f.BelegAsync(vorgang.BelegId!.Value)).ExtraktionsQuelle.ShouldBe(ExtraktionsQuelle.Manuell);
    }

    [Fact]
    public async Task Nicht_konfiguriert_geht_weiter_ohne_Extraktion()
    {
        var di = new FakeDiClient { IstKonfiguriert = false };
        var f = fabrik.MitDi(di);

        var vorgang = await f.ImportiereAsync("rechnung.pdf", DiTestHilfen.Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        di.Aufrufe.ShouldBe(0);
        (await f.BelegAsync(vorgang.BelegId!.Value)).ExtraktionsQuelle.ShouldBe(ExtraktionsQuelle.Manuell);
    }

    [Fact]
    public async Task Ab_80_Prozent_gibt_es_genau_eine_Warnung_je_Monat()
    {
        var di = new FakeDiClient();
        var f = fabrik.MitDi(di);
        await f.DiEinstellenAsync(limit: 10, warnungAb: 80);
        await f.VerbrauchSetzenAsync(Januar, 7);

        await f.ImportiereAsync("a.pdf", DiTestHilfen.Pdf); // 7 → 8 = 80 %
        await f.ImportiereAsync("b.pdf", DiTestHilfen.Pdf); // 8 → 9 = 90 %

        (await f.VerbrauchAsync(Januar)).ShouldBe(9);
        var warnung = (await f.MeldungenAsync(BenachrichtigungArt.DiLimitWarnung)).ShouldHaveSingleItem();
        warnung.Titel.ShouldBe("Document Intelligence bei 80 %");
        warnung.Text.ShouldBe("8 von 10 Seiten im Januar verbraucht. Ab 100 % warten neue Belege in der Warteschlange.");
        warnung.Link.ShouldBe("/einstellungen/document-intelligence");
        warnung.Schweregrad.ShouldBe(Schweregrad.Warnung);
        warnung.EntdoppelungsSchluessel.ShouldBe("di-warnung-2026-01");

        // Im nächsten Monat gibt es wieder eine eigene Warnung.
        f.Zeit.Setze(new DateTimeOffset(2026, 2, 10, 10, 0, 0, TimeSpan.Zero));
        await f.VerbrauchSetzenAsync(Februar, 7);
        await f.ImportiereAsync("c.pdf", DiTestHilfen.Pdf);

        (await f.MeldungenAsync(BenachrichtigungArt.DiLimitWarnung)).Select(m => m.EntdoppelungsSchluessel)
            .ShouldBe(["di-warnung-2026-01", "di-warnung-2026-02"], ignoreOrder: true);
    }

    [Fact]
    public async Task Unter_der_Schwelle_gibt_es_keine_Warnung()
    {
        var f = fabrik.MitDi(new FakeDiClient());
        await f.DiEinstellenAsync(limit: 10, warnungAb: 80);
        await f.VerbrauchSetzenAsync(Januar, 6);

        await f.ImportiereAsync("a.pdf", DiTestHilfen.Pdf); // 70 %

        (await f.MeldungenAsync(BenachrichtigungArt.DiLimitWarnung)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Limit_erreicht_pausiert_ohne_Versuch_und_meldet_genau_einmal_auch_bei_zwei_Belegen()
    {
        var di = new FakeDiClient();
        var f = fabrik.MitDi(di);
        await f.DiEinstellenAsync(limit: 10);
        await f.VerbrauchSetzenAsync(Januar, 10);

        var erster = await f.ImportiereAsync("a.pdf", DiTestHilfen.Pdf);
        var zweiter = await f.ImportiereAsync("b.pdf", DiTestHilfen.Pdf);

        foreach (var vorgang in new[] { erster, zweiter })
        {
            vorgang.Status.ShouldBe(ImportStatus.Pausiert);
            vorgang.Versuche.ShouldBe(0);
            vorgang.Ergebnis.ShouldBe("Monatslimit erreicht");
            vorgang.NaechsterVersuchAm.ShouldBeNull();
        }

        di.Aufrufe.ShouldBe(0);
        f.Planer().Verzoegert.ShouldBeEmpty();
        (await f.VerbrauchAsync(Januar)).ShouldBe(10);
        var meldung = (await f.MeldungenAsync(BenachrichtigungArt.DiLimitErreicht)).ShouldHaveSingleItem();
        meldung.Titel.ShouldBe("DI-Limit erreicht – Warteschlange pausiert");
        meldung.NurAdmins.ShouldBeTrue();
        meldung.EntdoppelungsSchluessel.ShouldBe("di-limit-2026-01");

        var verbrauch = await f.VerbrauchLesenAsync();
        verbrauch.Wartend.ShouldBe(2);
        verbrauch.Pausiert.ShouldBeTrue();
        verbrauch.Prozent.ShouldBe(100);
    }

    [Fact]
    public async Task Beleg_der_nicht_mehr_ins_Limit_passt_pausiert()
    {
        var di = new FakeDiClient { Seiten = 3 };
        var f = fabrik.MitDi(di, seiten: 3);
        await f.DiEinstellenAsync(limit: 10);
        await f.VerbrauchSetzenAsync(Januar, 8);

        var vorgang = await f.ImportiereAsync("a.pdf", DiTestHilfen.Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Pausiert);
        di.Aufrufe.ShouldBe(0);
    }

    [Fact]
    public async Task Monatswechsel_Job_setzt_pausierte_Vorgaenge_fort_und_zaehlt_im_neuen_Monat()
    {
        var di = new FakeDiClient();
        var f = fabrik.MitDi(di);
        await f.DiEinstellenAsync(limit: 10);
        await f.VerbrauchSetzenAsync(Januar, 10);
        var erster = await f.ImportiereAsync("a.pdf", DiTestHilfen.Pdf);
        var zweiter = await f.ImportiereAsync("b.pdf", DiTestHilfen.Pdf);

        // 01.02.2026, 00:05 in Berlin = 31.01.2026, 23:05 UTC
        f.Zeit.Setze(new DateTimeOffset(2026, 2, 1, 0, 5, 0, TimeSpan.FromHours(1)));
        using (var scope = f.ErzeugeScope())
        {
            var job = scope.ServiceProvider.GetServices<IWiederkehrenderJob>().OfType<DiMonatswechselJob>().Single();
            job.Cron.ShouldBe("5 0 1 * *");
            await job.AusfuehrenAsync(CancellationToken.None);
        }

        (await f.VorgangAsync(erster.Id)).Status.ShouldBe(ImportStatus.Fertig);
        (await f.VorgangAsync(zweiter.Id)).Status.ShouldBe(ImportStatus.Fertig);
        di.Aufrufe.ShouldBe(2);
        (await f.VerbrauchAsync(Januar)).ShouldBe(10);
        (await f.VerbrauchAsync(Februar)).ShouldBe(2);
    }

    [Fact]
    public async Task Neuer_Monat_beginnt_bei_0()
    {
        var f = fabrik.MitDi(new FakeDiClient());
        await f.DiEinstellenAsync(limit: 10);
        await f.VerbrauchSetzenAsync(Januar, 10);

        (await f.VerbrauchLesenAsync()).ShouldBe(new Contracts.Dto.Einstellungen.DiVerbrauchDto("2026-01", 10, 10, 100, true, 0));

        f.Zeit.Setze(new DateTimeOffset(2026, 1, 31, 23, 0, 0, TimeSpan.Zero)); // in Berlin schon Februar
        (await f.VerbrauchLesenAsync()).ShouldBe(new Contracts.Dto.Einstellungen.DiVerbrauchDto("2026-02", 0, 10, 0, false, 0));
    }

    [Fact]
    public async Task Nicht_erreichbar_wartet_auf_einen_neuen_Versuch_ohne_zu_zaehlen()
    {
        var di = new FakeDiClient { Fehler = new DiVoruebergehendNichtVerfuegbarException("HTTP 503") };
        var f = fabrik.MitDi(di);

        var vorgang = await f.ImportiereAsync("a.pdf", DiTestHilfen.Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Wartet);
        vorgang.Versuche.ShouldBe(1);
        vorgang.Ergebnis.ShouldBe("Document Intelligence nicht erreichbar");
        f.Planer().Verzoegert.ShouldHaveSingleItem().ShouldBe((vorgang.Id, TimeSpan.FromMinutes(1)));
        (await f.VerbrauchAsync(Januar)).ShouldBe(0);

        // Beim nächsten Versuch ist Azure wieder da.
        di.Fehler = null;
        f.Zeit.Vorspulen(TimeSpan.FromMinutes(1));
        await f.Planer().AusfuehrenAsync(vorgang.Id);

        (await f.VorgangAsync(vorgang.Id)).Status.ShouldBe(ImportStatus.Fertig);
        (await f.VerbrauchAsync(Januar)).ShouldBe(1);
    }
}
