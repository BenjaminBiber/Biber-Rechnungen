using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Pruefungen;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class PruefungenSchrittTests(ApiFabrik fabrik)
{
    private DateOnly Heute => DateOnly.FromDateTime(fabrik.Zeit.GetUtcNow().UtcDateTime);

    [Fact]
    public async Task Fehlerfreier_Beleg_bekommt_Inbox_aber_nicht_zu_pruefen()
    {
        var beleg = await BelegAnlegenAsync();

        await AusfuehrenAsync(beleg.Id);

        var tags = await TagsAsync(beleg.Id);
        tags.ShouldContain(SystemIds.TagInbox);
        tags.ShouldNotContain(SystemIds.TagZuPruefen);
        var befunde = await BefundeAsync(beleg.Id);
        befunde.Select(b => b.Pruefung).ShouldBe(Enum.GetValues<PruefungArt>(), ignoreOrder: true);
        befunde.ShouldAllBe(b => b.Ergebnis != PruefErgebnis.Fehlgeschlagen);
        befunde.Single(b => b.Pruefung == PruefungArt.Kosit).Detail.ShouldBe("Keine E-Rechnung (gescanntes PDF)");
        befunde.ShouldAllBe(b => b.Zeitpunkt == fabrik.Zeit.GetUtcNow());
    }

    [Fact]
    public async Task Design_Fall_setzt_zu_pruefen_mit_genau_zwei_Befunden()
    {
        var beleg = await BelegAnlegenAsync(b =>
        {
            b.Positionen[1].Gesamt = 102.15m; // Positionen 219,85 statt 221,85
            b.DiKonfidenz = 0.74m;
        });

        await AusfuehrenAsync(beleg.Id);

        (await TagsAsync(beleg.Id)).ShouldContain(SystemIds.TagZuPruefen);
        var fehler = (await BefundeAsync(beleg.Id)).Where(b => b.Ergebnis == PruefErgebnis.Fehlgeschlagen)
            .OrderBy(b => b.Pruefung).Select(b => (b.Pruefung, b.Detail)).ToList();
        fehler.ShouldBe(
        [
            (PruefungArt.Positionssumme, "Positionen 219,85 € statt 221,85 € · Differenz 2,00 €, Toleranz 0,02 €"),
            (PruefungArt.Konfidenz, "Konfidenz 0,74 liegt unter der Schwelle 0,80"),
        ]);
    }

    [Fact]
    public async Task KoSIT_Fehler_aus_dem_Kontext_wird_Befund_und_Meldung()
    {
        var beleg = await BelegAnlegenAsync(b => b.ExtraktionsQuelle = ExtraktionsQuelle.XRechnung);

        await AusfuehrenAsync(beleg.Id, k => k.Kosit = new KositErgebnis(false, ["BR-DE-15 Käuferreferenz fehlt"]));

        (await TagsAsync(beleg.Id)).ShouldContain(SystemIds.TagZuPruefen);
        (await BefundeAsync(beleg.Id)).Single(b => b.Pruefung == PruefungArt.Kosit)
            .ShouldSatisfyAllConditions(
                b => b.Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen),
                b => b.Detail.ShouldBe("KoSIT: BR-DE-15 Käuferreferenz fehlt"));
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var meldung = await db.Benachrichtigungen.SingleAsync(m => m.EntdoppelungsSchluessel == $"kosit:{beleg.Id}");
        meldung.Art.ShouldBe(BenachrichtigungArt.KositFehler);
        meldung.Titel.ShouldBe("KoSIT-Validierung fehlgeschlagen");
        meldung.Link.ShouldBe($"/belege/{beleg.Id}/pruefung");
    }

    [Fact]
    public async Task Erneutes_Pruefen_ohne_Kontext_aktualisiert_Befunde_und_laesst_Tag_und_KoSIT_stehen()
    {
        var beleg = await BelegAnlegenAsync(b =>
        {
            b.ExtraktionsQuelle = ExtraktionsQuelle.XRechnung;
            b.Rechnungsnummer = null;
        });
        await AusfuehrenAsync(beleg.Id, k => k.Kosit = new KositErgebnis(false, ["BR-DE-15 Käuferreferenz fehlt"]));
        var vorher = await BefundeAsync(beleg.Id);
        vorher.Single(b => b.Pruefung == PruefungArt.Pflichtfelder).Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen);
        fabrik.Zeit.Vorspulen(TimeSpan.FromMinutes(5));

        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            var geladen = await db.Belege.SingleAsync(b => b.Id == beleg.Id);
            geladen.Rechnungsnummer = "RE-korrigiert";
            await scope.ServiceProvider.GetRequiredService<IPruefDienst>().PruefenAsync(geladen, null, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        var nachher = await BefundeAsync(beleg.Id);
        nachher.Count.ShouldBe(9);
        nachher.Single(b => b.Pruefung == PruefungArt.Pflichtfelder).Ergebnis.ShouldBe(PruefErgebnis.Ok);
        nachher.Single(b => b.Pruefung == PruefungArt.Kosit)
            .ShouldBeEquivalentTo(vorher.Single(b => b.Pruefung == PruefungArt.Kosit));
        nachher.Single(b => b.Pruefung == PruefungArt.Datum).Zeitpunkt.ShouldBe(fabrik.Zeit.GetUtcNow());
        (await TagsAsync(beleg.Id)).ShouldContain(SystemIds.TagZuPruefen);
    }

    [Fact]
    public async Task Erneutes_Pruefen_ohne_Kontext_setzt_zu_pruefen_nicht()
    {
        var beleg = await BelegAnlegenAsync(b => b.Rechnungsnummer = null);

        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            var geladen = await db.Belege.SingleAsync(b => b.Id == beleg.Id);
            await scope.ServiceProvider.GetRequiredService<IPruefDienst>().PruefenAsync(geladen, null, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        (await BefundeAsync(beleg.Id)).ShouldContain(b => b.Ergebnis == PruefErgebnis.Fehlgeschlagen);
        (await TagsAsync(beleg.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Wiederholung_setzt_den_Inbox_Tag_nicht_erneut()
    {
        var beleg = await BelegAnlegenAsync();

        await AusfuehrenAsync(beleg.Id, wiederholung: true);

        (await TagsAsync(beleg.Id)).ShouldNotContain(SystemIds.TagInbox);
        (await BefundeAsync(beleg.Id)).Count.ShouldBe(9);
    }

    [Fact]
    public async Task Zweiter_Lauf_legt_nichts_doppelt_an()
    {
        var beleg = await BelegAnlegenAsync(b => b.DiKonfidenz = 0.5m);

        await AusfuehrenAsync(beleg.Id);
        await AusfuehrenAsync(beleg.Id);

        (await TagsAsync(beleg.Id)).Count(t => t == SystemIds.TagZuPruefen).ShouldBe(1);
        (await BefundeAsync(beleg.Id)).Count.ShouldBe(9);
    }

    [Fact]
    public async Task Schwellen_und_Schalter_aus_den_Einstellungen_wirken()
    {
        var beleg = await BelegAnlegenAsync(b =>
        {
            b.DiKonfidenz = 0.74m;
            b.Rechnungsnummer = null;
        });
        var aktiv = Enum.GetValues<PruefungArt>().ToDictionary(a => a, a => a != PruefungArt.Pflichtfelder);

        await MitEinstellungenAsync(
            new PruefEinstellungen { KonfidenzSchwelle = 0.7m, Aktiv = aktiv },
            new UstEinstellungen { Status = UstStatus.Kleinunternehmer },
            () => AusfuehrenAsync(beleg.Id));

        var befunde = await BefundeAsync(beleg.Id);
        befunde.Count.ShouldBe(8);
        befunde.ShouldNotContain(b => b.Pruefung == PruefungArt.Pflichtfelder);
        befunde.Single(b => b.Pruefung == PruefungArt.Konfidenz).Ergebnis.ShouldBe(PruefErgebnis.Ok);
        befunde.Single(b => b.Pruefung == PruefungArt.Paragraph14).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
        (await TagsAsync(beleg.Id)).ShouldNotContain(SystemIds.TagZuPruefen);
    }

    [Fact]
    public async Task Neuer_Korrespondent_und_Dublette_aus_der_Datenbank()
    {
        var original = await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsnummer: $"RE-{Guid.NewGuid():N}"[..14]));
        var beleg = await BelegAnlegenAsync(b => b.DuplikatVonId = original.Id, k => k.Geprueft = false);

        await AusfuehrenAsync(beleg.Id);

        var befunde = await BefundeAsync(beleg.Id);
        befunde.Single(b => b.Pruefung == PruefungArt.Dublette).Detail.ShouldBe($"Dublettenverdacht zu {original.Rechnungsnummer}");
        befunde.Single(b => b.Pruefung == PruefungArt.NeuerKorrespondent).Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen);
        (await TagsAsync(beleg.Id)).ShouldContain(SystemIds.TagZuPruefen);
    }

    [Fact]
    public void Schritt_ist_an_Position_Pruefungen_registriert()
    {
        using var scope = fabrik.ErzeugeScope();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<PruefungenSchritt>().Single();

        schritt.Reihenfolge.ShouldBe(SchrittReihenfolge.Pruefungen);
        schritt.Name.ShouldBe("Prüfungen");
    }

    private async Task<Beleg> BelegAnlegenAsync(Action<Beleg>? belegAendern = null, Action<Korrespondent>? korrespondentAendern = null)
    {
        var korrespondent = Testdaten.Korrespondent(ustIdNr: "DE" + Random.Shared.NextInt64(100_000_000, 999_999_999));
        korrespondent.Anschrift = new Anschrift { Strasse = "Hafenstraße 12", Plz = "20457", Ort = "Hamburg" };
        korrespondentAendern?.Invoke(korrespondent);
        await fabrik.SpeichernAsync(korrespondent);

        var beleg = Testdaten.Beleg(rechnungsdatum: Heute.AddDays(-5), korrespondentId: korrespondent.Id, status: BelegStatus.InVerarbeitung);
        beleg.Leistungsdatum = beleg.Rechnungsdatum;
        beleg.Netto = 221.85m;
        beleg.Steuer = 42.15m;
        beleg.Brutto = 264.00m;
        beleg.ExtraktionsQuelle = ExtraktionsQuelle.DocumentIntelligence;
        beleg.DiKonfidenz = 0.93m;
        beleg.Steuerzeilen = [new Steuerzeile { BelegId = beleg.Id, Satz = 19m, Netto = 221.85m, Steuer = 42.15m }];
        beleg.Positionen =
        [
            new Position { BelegId = beleg.Id, Nr = 1, Text = "Kopierpapier", Gesamt = 117.70m, Steuersatz = 19m },
            new Position { BelegId = beleg.Id, Nr = 2, Text = "Ordner", Gesamt = 104.15m, Steuersatz = 19m },
        ];
        belegAendern?.Invoke(beleg);
        return await fabrik.SpeichernAsync(beleg);
    }

    /// <summary>Lädt den Beleg wie der Orchestrator und führt Schritt 8 aus.</summary>
    private async Task AusfuehrenAsync(Guid belegId, Action<VerarbeitungsKontext>? kontextAendern = null, bool wiederholung = false)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var beleg = await db.Belege.Include(b => b.Steuerzeilen).Include(b => b.Positionen).SingleAsync(b => b.Id == belegId);
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<PruefungenSchritt>().Single();
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = new ImportVorgang { Id = Guid.NewGuid(), Dateiname = "beleg.pdf" },
            Beleg = beleg,
            DateiPfad = "beleg.pdf",
            Dateiname = "beleg.pdf",
            IstWiederholung = wiederholung,
        };
        kontextAendern?.Invoke(kontext);

        var ergebnis = await schritt.AusfuehrenAsync(kontext, CancellationToken.None);
        await db.SaveChangesAsync();

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
    }

    private async Task MitEinstellungenAsync(PruefEinstellungen pruef, UstEinstellungen ust, Func<Task> aktion)
    {
        using var scope = fabrik.ErzeugeScope();
        var einstellungen = scope.ServiceProvider.GetRequiredService<IEinstellungen>();
        var altPruef = await einstellungen.LesenAsync<PruefEinstellungen>(CancellationToken.None);
        var altUst = await einstellungen.LesenAsync<UstEinstellungen>(CancellationToken.None);
        await einstellungen.SpeichernAsync(pruef, CancellationToken.None);
        await einstellungen.SpeichernAsync(ust, CancellationToken.None);
        try
        {
            await aktion();
        }
        finally
        {
            await einstellungen.SpeichernAsync(altPruef, CancellationToken.None);
            await einstellungen.SpeichernAsync(altUst, CancellationToken.None);
        }
    }

    private async Task<List<Guid>> TagsAsync(Guid belegId)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.BelegTags.Where(t => t.BelegId == belegId).Select(t => t.TagId).ToListAsync();
    }

    private async Task<List<PruefBefund>> BefundeAsync(Guid belegId)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.PruefBefunde.AsNoTracking().Where(b => b.BelegId == belegId).OrderBy(b => b.Pruefung).ToListAsync();
    }
}
