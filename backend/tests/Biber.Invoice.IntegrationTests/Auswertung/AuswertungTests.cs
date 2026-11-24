using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Auswertung;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.Auswertung;

/// <summary>
/// Dashboard und Kostenauswertung gegen einen festen Bestand über 2025 und 2026; „heute“ ist der 23.11.2026.
/// Alle Erwartungswerte sind von Hand gerechnet (Steuer = Brutto − Brutto/1,19 gerundet, siehe <see cref="Testdaten"/>).
/// </summary>
[Collection("Api")]
public sealed class AuswertungTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateTimeOffset Heute = new(2026, 11, 23, 10, 0, 0, TimeSpan.Zero);

    // „Inbox“ und „zu prüfen“ legt der Seed an.
    private readonly Tag _inbox = new() { Id = SystemIds.TagInbox };
    private readonly Tag _zuPruefen = new() { Id = SystemIds.TagZuPruefen };
    private readonly Tag _bezahlt = new() { Id = Guid.NewGuid(), Name = "bezahlt", Farbe = "gruen", IstZahlungsTag = true };

    private readonly Kategorie _buero = Testdaten.Kategorie("Büro", "#1F6FEB");
    private readonly Kategorie _software = Testdaten.Kategorie("Software", "#7C3AED");

    private readonly Korrespondent _alpha = Testdaten.Korrespondent("Alpha GmbH");
    private readonly Korrespondent _beta = Testdaten.Korrespondent("Beta AG");
    private readonly Korrespondent _gamma = Testdaten.Korrespondent("Gamma KG");
    private readonly Korrespondent _delta = Testdaten.Korrespondent("Delta OHG");
    private readonly Korrespondent _epsilon = Testdaten.Korrespondent("Epsilon SE");
    private readonly Korrespondent _zeta = Testdaten.Korrespondent("Zeta UG");
    private readonly Korrespondent _eta = Testdaten.Korrespondent("Eta e.K.");

    private Beleg _ueberfaellig = null!;
    private Beleg _offen = null!;
    private Beleg _bezahltTrotzFaelligkeit = null!;
    private Beleg _altUeberfaellig = null!;
    private Artikel _papier = null!;
    private Artikel _toner = null!;
    private Artikel _kaffee = null!;

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(Heute);
        await fabrik.ZuruecksetzenAsync();
        await StandardEinstellungenAsync();
        await BestandAnlegenAsync();
    }

    // Einstellungen werden zwischengespeichert und überleben das Zurücksetzen der Datenbank.
    public Task DisposeAsync() => StandardEinstellungenAsync();

    [Fact]
    public async Task Kosten_des_Monats_mit_Vorjahresvergleich()
    {
        var dashboard = await DashboardAsync("Monat");

        // Nov 2026: 100 + 200 + 50 − 30 (Gutschrift); Dublette, nicht fertig und USD zählen nicht. Nov 2025: 200.
        dashboard.Kosten.ShouldBe(new DashboardKennzahlDto(4, 320.00m, 60.0m, "Nov 2025", null));
    }

    [Fact]
    public async Task Kosten_des_Quartals_und_des_Jahres()
    {
        // Q4 2026: 320 + 400 (Dezember) = 720; Q4 2025: 200 + 100 = 300.
        (await DashboardAsync("Quartal")).Kosten.ShouldBe(new DashboardKennzahlDto(5, 720.00m, 140.0m, "Q4 2025", null));

        // 2026: 1650 bis heute + 400 = 2050; 2025: 200 + 500 + 100 + 80 = 880 → +132,95 %.
        (await DashboardAsync("Jahr")).Kosten.ShouldBe(new DashboardKennzahlDto(9, 2050.00m, 133.0m, "2025", null));
    }

    [Fact]
    public async Task Kosten_des_Jahres_bis_heute_vergleichen_denselben_Abschnitt_des_Vorjahres()
    {
        var dashboard = await DashboardAsync("Monat");

        // 01.01.–23.11.2026: 320 + 1000 + 250 + 60 + 20 = 1650 (Dezember zählt nicht);
        // 01.01.–23.11.2025: 200 + 500 + 80 = 780 (Dezember 2025 zählt nicht) → +111,54 %.
        dashboard.KostenJahr.ShouldBe(new DashboardKennzahlDto(8, 1650.00m, 111.5m, "Jan–Nov 2025", null));
    }

    [Fact]
    public async Task Offen_und_Ueberfaellig_ueber_alle_Jahre()
    {
        var dashboard = await DashboardAsync("Monat");

        // Offen: 100 (fällig 01.11.2026), 200 (fällig 05.12.2026), 80 (fällig 01.07.2025); der bezahlte Beleg zählt nicht.
        dashboard.Offen.ShouldBe(new DashboardKennzahlDto(3, 380.00m, null, null, null));
        // Überfällig seit dem 01.07.2025 = 510 Tage.
        dashboard.Ueberfaellig.ShouldBe(new DashboardKennzahlDto(2, 180.00m, null, null, 510));
    }

    [Fact]
    public async Task Zahlungs_Tag_nimmt_den_Beleg_aus_Offen_und_Ueberfaellig()
    {
        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            db.BelegTags.Add(new BelegTag { BelegId = _ueberfaellig.Id, TagId = _bezahlt.Id, GesetztAm = Heute });
            await db.SaveChangesAsync();
        }

        var dashboard = await DashboardAsync("Monat");

        dashboard.Offen.ShouldBe(new DashboardKennzahlDto(2, 280.00m, null, null, null));
        dashboard.Ueberfaellig.ShouldBe(new DashboardKennzahlDto(1, 80.00m, null, null, 510));
        dashboard.OffeneBelege.Select(b => b.BelegId).ShouldNotContain(_ueberfaellig.Id);
    }

    [Fact]
    public async Task Inbox_und_zu_pruefen_zaehlen_ohne_Dubletten()
    {
        var dashboard = await DashboardAsync("Monat");

        dashboard.Inbox.ShouldBe(new DashboardKennzahlDto(2, null, null, null, null));
        dashboard.ZuPruefen.ShouldBe(new DashboardKennzahlDto(2, null, null, null, null));
    }

    [Fact]
    public async Task Kosten_pro_Monat_mit_Vorjahr_und_Kategorien()
    {
        var monate = (await DashboardAsync("Monat")).KostenProMonat;

        monate.Select(m => m.Monat).ShouldBe(Enumerable.Range(1, 12).Select(m => $"2026-{m:D2}"));
        monate.Select(m => m.Summe).ShouldBe([60m, 20m, 1000m, 0m, 0m, 250m, 0m, 0m, 0m, 0m, 320m, 400m]);
        monate.Select(m => m.SummeVorjahr).ShouldBe([0m, 0m, 500m, 0m, 0m, 80m, 0m, 0m, 0m, 0m, 200m, 100m]);
        foreach (var monat in monate)
        {
            monat.NachKategorie.Sum(k => k.Summe).ShouldBe(monat.Summe, monat.Monat);
        }

        var november = monate[10].NachKategorie;
        november.Select(k => (k.Kategorie?.Name, k.Farbe, k.Summe)).ShouldBe(
        [
            ("Software", "#7C3AED", 200m),
            ("Büro", "#1F6FEB", 70m),
            (null, "#9AA1AD", 50m),
        ]);
        monate[3].NachKategorie.ShouldBeEmpty();
    }

    [Fact]
    public async Task Top_Korrespondenten_des_Jahres()
    {
        var top = (await DashboardAsync("Monat")).TopKorrespondenten;

        top.Select(t => (t.Korrespondent.Name, t.Summe, t.AnzahlBelege)).ShouldBe(
        [
            ("Gamma KG", 1000m, 1),
            ("Epsilon SE", 400m, 1),
            ("Alpha GmbH", 300m, 2),
            ("Delta OHG", 250m, 1),
            ("Zeta UG", 60m, 1),
        ]);
        top[0].Logo.ShouldBe(new Contracts.Dto.LogoDto(null, "GK", LogoQuelle.Keine));
    }

    [Fact]
    public async Task Zu_pruefen_Liste_mit_Gruenden()
    {
        var liste = (await DashboardAsync("Monat")).ZuPruefenListe;

        liste.ShouldBe(
        [
            new ZuPruefenEintragDto(_offen.Id, "Alpha GmbH", _offen.Rechnungsnummer, 200m, "Summe weicht ab · Fälligkeit fehlt"),
            new ZuPruefenEintragDto(_bezahltTrotzFaelligkeit.Id, "Beta AG", _bezahltTrotzFaelligkeit.Rechnungsnummer, 50m, ""),
        ]);
    }

    [Fact]
    public async Task Offene_Belege_zuerst_ueberfaellig_dann_naechste_Faelligkeit()
    {
        var liste = (await DashboardAsync("Monat")).OffeneBelege;

        liste.Select(b => (b.BelegId, b.Faelligkeitsdatum, b.Ueberfaellig)).ShouldBe(
        [
            (_altUeberfaellig.Id, (DateOnly?)new DateOnly(2025, 7, 1), true),
            (_ueberfaellig.Id, new DateOnly(2026, 11, 1), true),
            (_offen.Id, new DateOnly(2026, 12, 5), false),
        ]);
        liste[0].Korrespondent.ShouldBe("Beta AG");
        liste[0].Brutto.ShouldBe(80m);
    }

    [Fact]
    public async Task Preisaenderungen_mit_Vorzeichen()
    {
        var liste = (await DashboardAsync("Monat")).Preisaenderungen;

        liste.ShouldBe(
        [
            new PreisaenderungDto(_toner.Id, "Toner", false, "Alpha GmbH", 80.00m, 70.00m, "Stk", -12.5m),
            new PreisaenderungDto(_papier.Id, "Papier A4", false, "Alpha GmbH", 5.00m, 5.50m, "Pck", 10.0m),
            new PreisaenderungDto(_kaffee.Id, "Kaffee", false, "Alpha GmbH", 10.00m, 10.20m, "kg", 2.0m),
        ]);
    }

    [Fact]
    public async Task Vorsteuer_je_Quartal()
    {
        var vorsteuer = (await DashboardAsync("Monat")).Vorsteuer.ShouldNotBeNull();

        vorsteuer.Zeitraum.ShouldBe(UstvaZeitraum.Quartalsweise);
        vorsteuer.Zeilen.ShouldBe(
        [
            new VorsteuerZeileDto("Q1 2026", 172.43m), // 9,58 + 3,19 + 159,66
            new VorsteuerZeileDto("Q2 2026", 39.92m),
            new VorsteuerZeileDto("Q3 2026", 0m),
            new VorsteuerZeileDto("Q4 2026", 114.96m), // 15,97 + 31,93 + 7,98 − 4,79 + 63,87
        ]);
        vorsteuer.Summe.ShouldBe(327.31m);
    }

    [Fact]
    public async Task Vorsteuer_entfaellt_bei_Kleinunternehmer()
    {
        await EinstellungSpeichernAsync(new UstEinstellungen { Status = UstStatus.Kleinunternehmer });

        (await DashboardAsync("Monat")).Vorsteuer.ShouldBeNull();
    }

    [Fact]
    public async Task Di_Verbrauch_des_laufenden_Monats()
    {
        (await DashboardAsync("Monat")).DiVerbrauch.ShouldBe(new DiVerbrauchDto("2026-11", 120, 500, 24, true, 2));
    }

    [Fact]
    public async Task Vergangenes_Jahr_nimmt_den_Dezember_als_Stichtag()
    {
        var dashboard = await DashboardAsync("Monat", 2025);

        dashboard.Kosten.ShouldBe(new DashboardKennzahlDto(1, 100.00m, null, "Dez 2024", null));
        dashboard.KostenJahr.ShouldBe(new DashboardKennzahlDto(4, 880.00m, null, "Jan–Dez 2024", null));
        dashboard.KostenProMonat[0].Monat.ShouldBe("2025-01");
    }

    [Fact]
    public async Task Kosten_nach_Monat()
    {
        var auswertung = await KostenAsync(KostenGruppierung.Monat);

        auswertung.Zeilen.ShouldBe(
        [
            new KostenZeileDto("2026-01", "Januar 2026", 50.42m, 9.58m, 60m, 1),
            new KostenZeileDto("2026-02", "Februar 2026", 16.81m, 3.19m, 20m, 1),
            new KostenZeileDto("2026-03", "März 2026", 840.34m, 159.66m, 1000m, 1),
            new KostenZeileDto("2026-06", "Juni 2026", 210.08m, 39.92m, 250m, 1),
            new KostenZeileDto("2026-11", "November 2026", 268.91m, 51.09m, 320m, 4),
        ]);
        auswertung.SummeBrutto.ShouldBe(1650m);
    }

    [Fact]
    public async Task Kosten_nach_Kategorie()
    {
        var auswertung = await KostenAsync(KostenGruppierung.Kategorie);

        auswertung.Zeilen.ShouldBe(
        [
            new KostenZeileDto(_buero.Id.ToString(), "Büro", 915.97m, 174.03m, 1090m, 4),
            new KostenZeileDto(_software.Id.ToString(), "Software", 428.57m, 81.43m, 510m, 3),
            new KostenZeileDto("ohne", "Ohne Kategorie", 42.02m, 7.98m, 50m, 1),
        ]);
        auswertung.SummeBrutto.ShouldBe(1650m);
    }

    [Fact]
    public async Task Kosten_nach_Korrespondent()
    {
        var auswertung = await KostenAsync(KostenGruppierung.Korrespondent);

        auswertung.Zeilen.Select(z => (z.Bezeichnung, z.Brutto, z.AnzahlBelege)).ShouldBe(
        [
            ("Gamma KG", 1000m, 1),
            ("Alpha GmbH", 300m, 2),
            ("Delta OHG", 250m, 1),
            ("Zeta UG", 60m, 1),
            ("Beta AG", 20m, 2),
            ("Eta e.K.", 20m, 1),
        ]);
        auswertung.Zeilen[0].Schluessel.ShouldBe(_gamma.Id.ToString());
        auswertung.SummeBrutto.ShouldBe(1650m);
    }

    [Fact]
    public async Task Ungueltiger_Zeitraum_liefert_400()
    {
        var antwort = await fabrik.ErzeugeClient().GetAsync($"{ApiRouten.Auswertung.Dashboard}?zeitraum=Woche");

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NurLesen_darf_das_Dashboard_sehen()
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync(ApiRouten.Auswertung.Dashboard);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(ApiRouten.Auswertung.Dashboard)]
    [InlineData(ApiRouten.SystemInfo.Status)]
    public async Task Anonym_liefert_401(string route)
    {
        var antwort = await fabrik.ErzeugeAnonymenClient().GetAsync(route);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Systemstatus_sammelt_alle_Quellen_und_ueberlebt_eine_fehlerhafte()
    {
        var mitFakes = fabrik.MitDiensten(d =>
        {
            d.RemoveAll<ISystemStatusQuelle>();
            d.AddSingleton<ISystemStatusQuelle>(new FesteQuelle("zz-ok"));
            d.AddSingleton<ISystemStatusQuelle>(new WerfendeQuelle("aa-wirft"));
        });

        var antwort = await mitFakes.ErzeugeClient().GetAsync(ApiRouten.SystemInfo.Status);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var eintraege = (await antwort.Content.ReadFromJsonAsync<SystemStatusEintragDto[]>())!;
        eintraege.Select(e => (e.Schluessel, e.Schweregrad)).ShouldBe([("aa-wirft", Schweregrad.Fehler), ("zz-ok", Schweregrad.Ok)]);
        eintraege[1].Titel.ShouldBe("Alles gut");
    }

    [Fact]
    public async Task Systemstatus_ohne_Quellen_ist_leer()
    {
        var ohneQuellen = fabrik.MitDiensten(d => d.RemoveAll<ISystemStatusQuelle>());

        var eintraege = await ohneQuellen.ErzeugeClient()
            .GetFromJsonAsync<SystemStatusEintragDto[]>(ApiRouten.SystemInfo.Status);

        eintraege.ShouldBeEmpty();
    }

    private async Task<DashboardDto> DashboardAsync(string zeitraum, int? jahr = null)
    {
        var url = $"{ApiRouten.Auswertung.Dashboard}?zeitraum={zeitraum}" + (jahr is null ? "" : $"&jahr={jahr}");
        var antwort = await fabrik.ErzeugeClient().GetAsync(url);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await antwort.Content.ReadFromJsonAsync<DashboardDto>())!;
    }

    private async Task<KostenAuswertungDto> KostenAsync(KostenGruppierung gruppierung)
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<IAuswertungsDienst>()
            .KostenAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 11, 30), gruppierung, CancellationToken.None);
    }

    private async Task StandardEinstellungenAsync()
    {
        await EinstellungSpeichernAsync(new AllgemeinEinstellungen());
        await EinstellungSpeichernAsync(new UstEinstellungen());
        await EinstellungSpeichernAsync(new DiEinstellungen());
    }

    private async Task EinstellungSpeichernAsync<T>(T wert)
        where T : class
    {
        using var scope = fabrik.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<IEinstellungen>().SpeichernAsync(wert, CancellationToken.None);
    }

    private async Task BestandAnlegenAsync()
    {
        _papier = Artikel("Papier A4", "Pck");
        _toner = Artikel("Toner", "Stk");
        _kaffee = Artikel("Kaffee", "kg");
        var lizenz = Artikel("Lizenz", "Stk");

        // 2026
        _ueberfaellig = Beleg(_alpha, _buero, new(2026, 11, 5), 100m, faellig: new(2026, 11, 1), tags: [_inbox]);
        _offen = Beleg(_alpha, _software, new(2026, 11, 20), 200m, faellig: new(2026, 12, 5), tags: [_zuPruefen]);
        _offen.ErstelltAm = new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero);
        _offen.Befunde.AddRange(
        [
            Befund(_offen, PruefungArt.Positionssumme, PruefErgebnis.Fehlgeschlagen, "Summe weicht ab", 1),
            Befund(_offen, PruefungArt.Pflichtfelder, PruefErgebnis.Ok, "Pflichtfelder vollständig", 2),
            Befund(_offen, PruefungArt.Datum, PruefErgebnis.Fehlgeschlagen, "Fälligkeit fehlt", 3),
        ]);
        _bezahltTrotzFaelligkeit = Beleg(_beta, null, new(2026, 11, 10), 50m, faellig: new(2026, 10, 1), tags: [_bezahlt, _zuPruefen]);
        _bezahltTrotzFaelligkeit.ErstelltAm = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);
        var gutschrift = Beleg(_beta, _buero, new(2026, 11, 15), 30m);
        gutschrift.Belegart = Belegart.Gutschrift;

        var dublette = Beleg(_alpha, _buero, new(2026, 11, 6), 999m, tags: [_inbox, _zuPruefen]);
        dublette.DuplikatVonId = _ueberfaellig.Id;
        dublette.ErstelltAm = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);
        var nichtFertig = Beleg(_alpha, _buero, new(2026, 11, 7), 888m, faellig: new(2026, 11, 2));
        nichtFertig.Status = BelegStatus.InVerarbeitung;
        var usd = Beleg(_alpha, _buero, new(2026, 11, 8), 777m, faellig: new(2026, 11, 2));
        usd.Waehrung = "USD";

        var gammaMaerz = Beleg(_gamma, _buero, new(2026, 3, 10), 1000m, bezahlt: true, tags: [_inbox]);
        var deltaJuni = Beleg(_delta, _software, new(2026, 6, 15), 250m, bezahlt: true);
        var epsilonDezember = Beleg(_epsilon, _buero, new(2026, 12, 1), 400m, faellig: new(2026, 12, 15), bezahlt: true);
        var zetaJanuar = Beleg(_zeta, _software, new(2026, 1, 20), 60m, bezahlt: true);
        var etaFebruar = Beleg(_eta, _buero, new(2026, 2, 2), 20m, bezahlt: true);

        // 2025
        var alphaNov2025 = Beleg(_alpha, _buero, new(2025, 11, 12), 200m, bezahlt: true);
        var gammaMaerz2025 = Beleg(_gamma, null, new(2025, 3, 1), 500m, bezahlt: true);
        var alphaDez2025 = Beleg(_alpha, null, new(2025, 12, 10), 100m, bezahlt: true);
        _altUeberfaellig = Beleg(_beta, null, new(2025, 6, 1), 80m, faellig: new(2025, 7, 1));

        // Preise 2026: Papier 5,00 → 5,50; Toner 80 → 60 → 70; Kaffee 10,00 → 10,20; Lizenz nur einmal.
        Kauf(zetaJanuar, _papier, 5.00m);
        Kauf(_ueberfaellig, _papier, 5.50m);
        Kauf(dublette, _papier, 100m);
        Kauf(etaFebruar, _toner, 80.00m);
        Kauf(deltaJuni, _toner, 60.00m);
        Kauf(_offen, _toner, 70.00m);
        Kauf(gammaMaerz, _kaffee, 10.00m);
        Kauf(_ueberfaellig, _kaffee, 10.20m);
        Kauf(alphaNov2025, _kaffee, 1.00m);
        Kauf(_offen, lizenz, 99m);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        db.AddRange(_bezahlt, _buero, _software);
        db.AddRange(_alpha, _beta, _gamma, _delta, _epsilon, _zeta, _eta);
        db.AddRange(_papier, _toner, _kaffee, lizenz);
        db.AddRange(
            _ueberfaellig, _offen, _bezahltTrotzFaelligkeit, gutschrift, dublette, nichtFertig, usd,
            gammaMaerz, deltaJuni, epsilonDezember, zetaJanuar, etaFebruar,
            alphaNov2025, gammaMaerz2025, alphaDez2025, _altUeberfaellig);
        db.DiVerbraeuche.AddRange(
            new DiVerbrauch { Monat = new DateOnly(2026, 11, 1), Seiten = 120 },
            new DiVerbrauch { Monat = new DateOnly(2026, 10, 1), Seiten = 480 });
        db.ImportVorgaenge.AddRange(Vorgang(ImportStatus.Pausiert), Vorgang(ImportStatus.Pausiert), Vorgang(ImportStatus.Fertig));
        await db.SaveChangesAsync();
    }

    private Beleg Beleg(
        Korrespondent korrespondent,
        Kategorie? kategorie,
        DateOnly datum,
        decimal brutto,
        DateOnly? faellig = null,
        bool bezahlt = false,
        Tag[]? tags = null)
    {
        var beleg = Testdaten.Beleg(rechnungsdatum: datum, brutto: brutto, korrespondentId: korrespondent.Id, kategorieId: kategorie?.Id);
        beleg.Faelligkeitsdatum = faellig;
        foreach (var tag in (tags ?? []).Concat(bezahlt ? [_bezahlt] : []))
        {
            beleg.Tags.Add(new BelegTag { BelegId = beleg.Id, TagId = tag.Id, GesetztAm = Heute });
        }

        return beleg;
    }

    private Artikel Artikel(string name, string einheit) => new()
    {
        Id = Guid.NewGuid(),
        KorrespondentId = _alpha.Id,
        Name = name,
        NameNormalisiert = name.ToLowerInvariant(),
        Einheit = einheit,
    };

    private static void Kauf(Beleg beleg, Artikel artikel, decimal einzelpreis) =>
        beleg.Positionen.Add(new Position
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Nr = beleg.Positionen.Count + 1,
            Text = artikel.Name,
            Menge = 1,
            Einheit = artikel.Einheit,
            Einzelpreis = einzelpreis,
            Gesamt = einzelpreis,
            ArtikelId = artikel.Id,
        });

    private static PruefBefund Befund(Beleg beleg, PruefungArt art, PruefErgebnis ergebnis, string detail, int minute) => new()
    {
        Id = Guid.NewGuid(),
        BelegId = beleg.Id,
        Pruefung = art,
        Ergebnis = ergebnis,
        Detail = detail,
        Zeitpunkt = new DateTimeOffset(2026, 11, 21, 8, minute, 0, TimeSpan.Zero),
    };

    private static ImportVorgang Vorgang(ImportStatus status) => new()
    {
        Id = Guid.NewGuid(),
        Quelle = ImportQuelle.Upload,
        Status = status,
        Dateiname = "beleg.pdf",
        ErstelltAm = Heute,
    };

    private sealed class FesteQuelle(string schluessel) : ISystemStatusQuelle
    {
        public string Schluessel => schluessel;

        public Task<SystemStatusEintragDto> LesenAsync(CancellationToken ct) =>
            Task.FromResult(new SystemStatusEintragDto(schluessel, Schweregrad.Ok, "Alles gut", "läuft", null));
    }

    private sealed class WerfendeQuelle(string schluessel) : ISystemStatusQuelle
    {
        public string Schluessel => schluessel;

        public Task<SystemStatusEintragDto> LesenAsync(CancellationToken ct) =>
            throw new InvalidOperationException("Quelle kaputt");
    }
}
