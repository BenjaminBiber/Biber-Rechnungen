using System.Collections.Concurrent;
using System.Security.Cryptography;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.HashDublette;
using Biber.Invoice.Infrastructure.Module.KorrespondentZuordnung;
using Biber.Invoice.Infrastructure.Module.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class PipelineTests(ApiFabrik fabrik)
{
    private static readonly byte[] Pdf = "%PDF-1.7 Testinhalt"u8.ToArray();

    [Fact]
    public async Task Annahme_legt_Staging_Datei_mit_korrektem_SHA256_an()
    {
        var f = OhneDublettenCheck(fabrik);

        var vorgang = await f.ImportiereAsync("Rechnung.PDF", Pdf);

        vorgang.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Pdf)));
        vorgang.Groesse.ShouldBe(Pdf.Length);
        vorgang.StagingPfad.ShouldNotBeNull();
        vorgang.StagingPfad.ShouldStartWith(Path.Combine(f.TempOrdner, "staging", "20260115"));
        Path.GetExtension(vorgang.StagingPfad).ShouldBe(".pdf");
        (await File.ReadAllBytesAsync(vorgang.StagingPfad)).ShouldBe(Pdf);
    }

    [Fact]
    public async Task Ohne_weitere_Schritte_endet_der_Vorgang_Fertig_mit_Beleg_Fertig()
    {
        var f = OhneDublettenCheck(fabrik);

        var vorgang = await f.ImportiereAsync("rechnung.pdf", Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        vorgang.Ergebnis.ShouldBe("1 Beleg angelegt");
        vorgang.Versuche.ShouldBe(0);
        vorgang.BeendetAm.ShouldBe(f.Zeit.GetUtcNow());
        vorgang.Schritte.ShouldAllBe(s => s.Status == "ok");
        var beleg = await BelegAsync(f, vorgang.BelegId!.Value);
        beleg.Status.ShouldBe(BelegStatus.Fertig);
        beleg.ImportVorgangId.ShouldBe(vorgang.Id);
        beleg.Quelle.ShouldBe(ImportQuelle.Upload);
        beleg.ExtraktionsQuelle.ShouldBe(ExtraktionsQuelle.Manuell);
    }

    [Fact]
    public async Task Schritte_laufen_in_der_Reihenfolge_ihrer_Konstanten()
    {
        var protokoll = new ConcurrentQueue<string>();
        var f = OhneDublettenCheck(fabrik).MitDiensten(d =>
        {
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.Pruefungen, "Prüfungen", protokoll));
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.HashDublette, "Dubletten-Check", protokoll));
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.Korrespondent, "Korrespondent", protokoll));
        });

        var vorgang = await f.ImportiereAsync("rechnung.pdf", Pdf);

        protokoll.ShouldBe(["Dubletten-Check", "Korrespondent", "Prüfungen"]);
        vorgang.Schritte.Select(s => s.Name).ShouldBe(["Dubletten-Check", "E-Rechnung lesen", "Daten übernehmen", "Korrespondent", "Positionen", "Prüfungen"]);
        vorgang.Status.ShouldBe(ImportStatus.Fertig);
    }

    [Fact]
    public async Task Beendet_stoppt_die_Pipeline_mit_dem_genannten_Status()
    {
        var protokoll = new ConcurrentQueue<string>();
        var f = OhneDublettenCheck(fabrik).MitDiensten(d =>
        {
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(
                SchrittReihenfolge.HashDublette, "Dubletten-Check", protokoll, _ => SchrittErgebnis.Beendet(ImportStatus.Dublette, "Dublette von 26-0912")));
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.Formaterkennung, "Format", protokoll));
        });

        var vorgang = await f.ImportiereAsync("rechnung.pdf", Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Dublette);
        vorgang.Ergebnis.ShouldBe("Dublette von 26-0912");
        protokoll.ShouldBe(["Dubletten-Check"]);
        vorgang.Schritte.Select(s => s.Status).ShouldBe(["ok", "offen", "offen", "offen", "offen"]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [InlineData(3, 10)]
    [InlineData(4, 20)]
    [InlineData(5, 30)]
    public void Backoff_wartet_1_4_10_20_30_Minuten(int versuch, int minuten)
    {
        VerarbeitungsJob.Wartezeit(versuch).ShouldBe(TimeSpan.FromMinutes(minuten));
    }

    [Fact]
    public async Task SpaeterErneut_wartet_mit_Backoff_und_endet_nach_5_Versuchen_im_Fehler_mit_Tag_zu_pruefen()
    {
        var meldungen = Substitute.For<IBenachrichtigungsDienst>();
        var protokoll = new ConcurrentQueue<string>();
        var f = OhneDublettenCheck(fabrik).MitDiensten(d =>
        {
            d.AddSingleton(meldungen);
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(
                SchrittReihenfolge.DocumentIntelligence, "Document Intelligence", protokoll, _ => SchrittErgebnis.SpaeterErneut("Document Intelligence nicht erreichbar")));
        });
        await TagZuPruefenSicherstellenAsync(f);

        var vorgang = await f.ImportiereAsync("rechnung.pdf", Pdf);

        int[] erwartet = [1, 4, 10, 20];
        for (var versuch = 1; versuch <= erwartet.Length; versuch++)
        {
            var wartezeit = TimeSpan.FromMinutes(erwartet[versuch - 1]);
            vorgang.Status.ShouldBe(ImportStatus.Wartet);
            vorgang.Versuche.ShouldBe(versuch);
            vorgang.Ergebnis.ShouldBe("Document Intelligence nicht erreichbar");
            vorgang.NaechsterVersuchAm.ShouldBe(f.Zeit.GetUtcNow() + wartezeit);
            f.Planer().Verzoegert[^1].ShouldBe((vorgang.Id, wartezeit));

            f.Zeit.Vorspulen(wartezeit);
            await f.Planer().AusfuehrenAsync(vorgang.Id);
            vorgang = await f.VorgangAsync(vorgang.Id);
        }

        protokoll.Count.ShouldBe(5); // jeder Versuch führt alle Schritte erneut aus
        vorgang.Status.ShouldBe(ImportStatus.Fehler);
        vorgang.Versuche.ShouldBe(VerarbeitungsJob.MaxVersuche);
        vorgang.Fehler.ShouldBe("Document Intelligence nicht erreichbar");
        vorgang.NaechsterVersuchAm.ShouldBeNull();
        f.Planer().Verzoegert.Count.ShouldBe(4);

        var beleg = await BelegAsync(f, vorgang.BelegId!.Value);
        beleg.Status.ShouldBe(BelegStatus.Fehler);
        beleg.Tags.Select(t => t.TagId).ShouldBe([SystemIds.TagZuPruefen]);
        await meldungen.Received(1).MeldenAsync(
            Arg.Is<NeueBenachrichtigung>(m => m.Art == BenachrichtigungArt.ImportFehler && m.Schweregrad == Schweregrad.Fehler),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Exception_eines_Schritts_zaehlt_als_Versuch_und_verwirft_dessen_Aenderungen()
    {
        var protokoll = new ConcurrentQueue<string>();
        var f = OhneDublettenCheck(fabrik).MitDiensten(d =>
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.Korrespondent, "Korrespondent", protokoll, k =>
            {
                k.Beleg.Rechnungsnummer = "halbfertig";
                throw new IOException("Datei gesperrt");
            })));

        var vorgang = await f.ImportiereAsync("rechnung.pdf", Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Wartet);
        vorgang.Versuche.ShouldBe(1);
        vorgang.Ergebnis.ShouldBe("Korrespondent: Datei gesperrt");
        vorgang.Schritte.Single(s => s.Name == "Korrespondent").Status.ShouldBe("fehler");
        (await BelegAsync(f, vorgang.BelegId!.Value)).Rechnungsnummer.ShouldBeNull();
    }

    [Fact]
    public async Task Pausiert_zaehlt_nicht_als_Versuch_und_PausierteFortsetzen_plant_neu_ein()
    {
        var pausieren = true;
        var protokoll = new ConcurrentQueue<string>();
        var f = OhneDublettenCheck(fabrik).MitDiensten(d =>
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.DocumentIntelligence, "Document Intelligence", protokoll, _ =>
                pausieren ? SchrittErgebnis.Pausiert("DI-Limit erreicht") : SchrittErgebnis.Weiter)));

        var vorgang = await f.ImportiereAsync("rechnung.pdf", Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Pausiert);
        vorgang.Versuche.ShouldBe(0);
        vorgang.Ergebnis.ShouldBe("DI-Limit erreicht");
        f.Planer().Verzoegert.ShouldBeEmpty();

        pausieren = false;
        int fortgesetzt;
        using (var scope = f.ErzeugeScope())
        {
            fortgesetzt = await scope.ServiceProvider.GetRequiredService<IImportDienst>().PausierteFortsetzenAsync(CancellationToken.None);
        }

        fortgesetzt.ShouldBeGreaterThanOrEqualTo(1);
        vorgang = await f.VorgangAsync(vorgang.Id);
        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        vorgang.Versuche.ShouldBe(0);
        protokoll.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData("foto_beleg.heic")]
    [InlineData("rechnung.docx")]
    [InlineData("ohne_endung")]
    public async Task Unzulaessiges_Format_wird_abgelehnt(string dateiname)
    {
        var meldungen = Substitute.For<IBenachrichtigungsDienst>();
        var f = OhneDublettenCheck(fabrik).MitDiensten(d => d.AddSingleton(meldungen));

        var vorgang = await f.ImportiereAsync(dateiname, Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Abgelehnt);
        vorgang.Fehler.ShouldBe("Format nicht unterstützt. Erlaubt sind PDF, XML, PNG, JPG und TIFF.");
        vorgang.StagingPfad.ShouldBeNull();
        vorgang.BelegId.ShouldBeNull();
        Directory.Exists(Path.Combine(f.TempOrdner, "staging")).ShouldBeFalse();
        await meldungen.Received(1).MeldenAsync(
            Arg.Is<NeueBenachrichtigung>(m => m.Art == BenachrichtigungArt.ImportFehler),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("a.XML")]
    [InlineData("a.png")]
    [InlineData("a.Jpg")]
    [InlineData("a.jpeg")]
    [InlineData("a.TIF")]
    [InlineData("a.tiff")]
    public async Task Zulaessige_Formate_werden_angenommen(string dateiname)
    {
        var f = OhneDublettenCheck(fabrik);

        var vorgang = await f.ImportiereAsync(dateiname, Pdf);

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
    }

    [Fact]
    public async Task ExtraktionUebernehmen_ist_idempotent()
    {
        var protokoll = new ConcurrentQueue<string>();
        var f = OhneDublettenCheck(fabrik).MitDiensten(d =>
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.DocumentIntelligence, "Document Intelligence", protokoll, k =>
            {
                k.Extraktion = Extraktion();
                return SchrittErgebnis.Weiter;
            })));
        var vorgang = await f.ImportiereAsync("rechnung.pdf", Pdf);
        vorgang.Status.ShouldBe(ImportStatus.Fertig);

        // Zweimal ausführen, wie bei einer Wiederholung
        for (var i = 0; i < 2; i++)
        {
            using var scope = f.ErzeugeScope();
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            var beleg = await db.Belege.Include(b => b.Positionen).Include(b => b.Steuerzeilen).SingleAsync(b => b.Id == vorgang.BelegId);
            var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<ExtraktionUebernehmenSchritt>().Single();
            var kontext = new VerarbeitungsKontext
            {
                Vorgang = vorgang,
                Beleg = beleg,
                DateiPfad = vorgang.StagingPfad!,
                Dateiname = vorgang.Dateiname,
                Extraktion = Extraktion(),
            };

            (await schritt.AusfuehrenAsync(kontext, CancellationToken.None)).ShouldBe(SchrittErgebnis.Weiter);
            await db.SaveChangesAsync();
        }

        var ergebnis = await BelegAsync(f, vorgang.BelegId!.Value);
        ergebnis.Positionen.Count.ShouldBe(2);
        ergebnis.Steuerzeilen.Count.ShouldBe(1);
        ergebnis.Rechnungsnummer.ShouldBe("RE-2026-0042");
        ergebnis.Brutto.ShouldBe(119.00m);
        ergebnis.ExtraktionsQuelle.ShouldBe(ExtraktionsQuelle.DocumentIntelligence);
        ergebnis.DiKonfidenz.ShouldBe(0.93m);
        ergebnis.ExtrahiertAm.ShouldBe(f.Zeit.GetUtcNow());
        ergebnis.Titel.ShouldBe(new string('W', 80));
    }

    [Fact]
    public async Task ErneutVerarbeiten_laeuft_ab_Formaterkennung_mit_dem_Original_aus_der_Ablage()
    {
        var protokoll = new ConcurrentQueue<string>();
        var kontexte = new ConcurrentQueue<VerarbeitungsKontext>();
        var f = OhneDublettenCheck(fabrik).MitDiensten(d =>
        {
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.HashDublette, "Dubletten-Check", protokoll));
            d.AddSingleton<IPipelineSchritt>(new TestSchritt(SchrittReihenfolge.Formaterkennung, "Format", protokoll, k =>
            {
                kontexte.Enqueue(k);
                k.Extraktion = Extraktion();
                return SchrittErgebnis.Weiter;
            }));
        });
        var vorgang = await f.ImportiereAsync("rechnung.pdf", Pdf);
        await f.SpeichernAsync(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = vorgang.BelegId!.Value,
            Rolle = DateiRolle.Original,
            Pfad = Path.Combine("2026", "_Unbekannt", "2026-01-10_RE-2026-0042.pdf"),
            Sha256 = vorgang.Sha256!,
            Groesse = vorgang.Groesse,
            MimeType = "application/pdf",
            AbgelegtAm = f.Zeit.GetUtcNow(),
        });

        using (var scope = f.ErzeugeScope())
        {
            await scope.ServiceProvider.GetRequiredService<IVerarbeitungsStarter>().ErneutVerarbeitenAsync(vorgang.BelegId!.Value, CancellationToken.None);
        }

        protokoll.ShouldBe(["Dubletten-Check", "Format", "Format"]);
        var wiederholung = kontexte.Last();
        wiederholung.IstWiederholung.ShouldBeTrue();
        wiederholung.DateiPfad.ShouldBe(Path.Combine(f.TempOrdner, "ablage", "2026", "_Unbekannt", "2026-01-10_RE-2026-0042.pdf"));
        var beleg = await BelegAsync(f, vorgang.BelegId!.Value);
        beleg.Positionen.Count.ShouldBe(2);
        beleg.Status.ShouldBe(BelegStatus.Fertig);
    }

    [Fact]
    public async Task Vorgang_zeigt_Herkunft_je_Quelle()
    {
        var f = OhneDublettenCheck(fabrik);
        var mail = await f.ImportiereAsync("anhang.pdf", Pdf, ImportQuelle.Mail, new ImportHerkunft(MailAbsender: "rechnung@stadtwerke.example", MailBetreff: "Ihre Rechnung 2026-1194"));
        var ordner = await f.ImportiereAsync("Scan_0915.pdf", Pdf, ImportQuelle.Ordner, new ImportHerkunft(Pfad: "/srv/rechnungen/eingang/Scan_0915.pdf"));

        using var scope = f.ErzeugeScope();
        var dienst = scope.ServiceProvider.GetRequiredService<IImportDienst>();
        var mailDto = (await dienst.VorgangAsync(mail.Id, CancellationToken.None))!;
        var ordnerDto = (await dienst.VorgangAsync(ordner.Id, CancellationToken.None))!;
        var liste = await dienst.VorgaengeAsync(ImportQuelle.Mail, 30, 1, CancellationToken.None);

        mailDto.Herkunft.ShouldBe("„Ihre Rechnung 2026-1194“");
        mailDto.HerkunftDetail.ShouldBe("von rechnung@stadtwerke.example");
        mailDto.MaxVersuche.ShouldBe(5);
        mailDto.BelegId.ShouldBe(mail.BelegId);
        ordnerDto.Herkunft.ShouldBe("Scan_0915.pdf");
        ordnerDto.HerkunftDetail.ShouldBe("Rechnungen-Eingang");
        liste.Eintraege.ShouldContain(v => v.Id == mail.Id);
        liste.Eintraege.ShouldAllBe(v => v.Quelle == ImportQuelle.Mail);
        (await dienst.VorgangAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public void Hangfire_Planer_reiht_den_Job_in_die_Queue_verarbeitung_ein()
    {
        var planer = fabrik.Services.GetRequiredService<IVerarbeitungsPlaner>();
        var speicher = fabrik.Services.GetRequiredService<JobStorage>();
        var vorher = speicher.GetMonitoringApi().EnqueuedCount(PipelineModul.QueueVerarbeitung);

        planer.ShouldBeOfType<HangfirePlaner>();
        planer.Einplanen(Guid.NewGuid());
        planer.Einplanen(Guid.NewGuid(), TimeSpan.FromMinutes(4));

        speicher.GetMonitoringApi().EnqueuedCount(PipelineModul.QueueVerarbeitung).ShouldBe(vorher + 1);
        speicher.GetMonitoringApi().ScheduledCount().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void Hangfire_Server_laeuft_in_Tests_nicht()
    {
        fabrik.Services.GetServices<IHostedService>()
            .ShouldNotContain(s => s.GetType().Name == "BackgroundJobServerHostedService");
        fabrik.Services.GetServices<IHostedService>().ShouldContain(s => s is WiederkehrendeJobsEintragen);
    }

    [Fact]
    public async Task Wiederkehrende_Jobs_werden_beim_Start_in_Hangfire_eingetragen()
    {
        var job = Substitute.For<IWiederkehrenderJob>();
        job.Id.Returns("test-job");
        job.Cron.Returns("*/5 * * * *");
        var f = fabrik.MitDiensten(d => d.AddSingleton(job));
        _ = f.Server;

        using var verbindung = f.Services.GetRequiredService<JobStorage>().GetConnection();
        verbindung.GetRecurringJobs(["test-job"]).Single().Cron.ShouldBe("*/5 * * * *");

        using var scope = f.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<WiederkehrenderJobAusfuehrer>().AusfuehrenAsync("test-job", CancellationToken.None);
        await job.Received(1).AusfuehrenAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Sofort-Verarbeitung ohne den echten Dubletten-Check: Diese Tests prüfen den Orchestrator mit eigenen Schritten
    /// und importieren dieselben Bytes mehrfach (der Check hat <see cref="HashDubletteTests"/>).
    /// </summary>
    private static ApiFabrik OhneDublettenCheck(ApiFabrik fabrik) =>
        fabrik.MitSofortVerarbeitung().MitDiensten(d =>
        {
            foreach (var schritt in d.Where(x => x.ServiceType == typeof(IPipelineSchritt) && (x.ImplementationType == typeof(HashDubletteSchritt) || x.ImplementationType == typeof(KorrespondentZuordnungSchritt))).ToList())
            {
                d.Remove(schritt);
            }
        });

    private static ExtraktionsErgebnis Extraktion() => new()
    {
        Quelle = ExtraktionsQuelle.DocumentIntelligence,
        Belegart = Belegart.Rechnung,
        Rechnungsnummer = "RE-2026-0042",
        Rechnungsdatum = new DateOnly(2026, 1, 10),
        Netto = 100.00m,
        Steuer = 19.00m,
        Brutto = 119.00m,
        Steuerzeilen = [new ExtrahierteSteuerzeile(19m, 100.00m, 19.00m)],
        Positionen =
        [
            new ExtrahiertePosition(1, new string('W', 120), 1m, "Stk", 60.00m, 60.00m, 19m),
            new ExtrahiertePosition(2, "Versand", 1m, null, 40.00m, 40.00m, 19m),
        ],
        Volltext = "Rechnung RE-2026-0042",
        Konfidenz = 0.93m,
        Seiten = 1,
    };

    /// <summary>
    /// Die Initial-Migration legt den System-Tag an; <see cref="ApiFabrik.ZuruecksetzenAsync"/> anderer Tests
    /// auf derselben Datenbank leert aber auch ihn.
    /// </summary>
    private static async Task TagZuPruefenSicherstellenAsync(ApiFabrik f)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        if (!await db.Tags.AnyAsync(t => t.Id == SystemIds.TagZuPruefen))
        {
            db.Tags.Add(new Tag { Id = SystemIds.TagZuPruefen, Name = "zu prüfen", Farbe = "orange", IstSystem = true });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<Beleg> BelegAsync(ApiFabrik f, Guid id)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Belege.AsNoTracking()
            .Include(b => b.Positionen)
            .Include(b => b.Steuerzeilen)
            .Include(b => b.Tags)
            .SingleAsync(b => b.Id == id);
    }

    /// <summary>Schritt für Tests: protokolliert seinen Namen und liefert das Ergebnis von <c>aktion</c> (Standard: Weiter).</summary>
    private sealed class TestSchritt(
        int reihenfolge,
        string name,
        ConcurrentQueue<string> protokoll,
        Func<VerarbeitungsKontext, SchrittErgebnis>? aktion = null) : IPipelineSchritt
    {
        public int Reihenfolge => reihenfolge;

        public string Name => name;

        public Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
        {
            protokoll.Enqueue(name);
            return Task.FromResult(aktion?.Invoke(kontext) ?? SchrittErgebnis.Weiter);
        }
    }
}
