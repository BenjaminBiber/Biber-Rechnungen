using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.IntegrationTests.Infrastruktur;

namespace Biber.Invoice.IntegrationTests.Belege;

[Collection("Api")]
public sealed class BelegLeseTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateTimeOffset Jetzt = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private Guid _zahlungsTagId;

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

        // Respawn leert auch die Systemdaten der Migration; die Tags „Inbox“ und „zu prüfen“ neu anlegen.
        var inbox = Testdaten.Tag("Inbox", "blau");
        inbox.Id = SystemIds.TagInbox;
        inbox.IstInbox = true;
        inbox.IstSystem = true;
        await fabrik.SpeichernAsync(inbox);
        var zuPruefen = Testdaten.Tag("zu prüfen", "orange");
        zuPruefen.Id = SystemIds.TagZuPruefen;
        zuPruefen.IstSystem = true;
        await fabrik.SpeichernAsync(zuPruefen);
        var bezahlt = Testdaten.Tag("bezahlt", "gruen");
        bezahlt.IstZahlungsTag = true;
        _zahlungsTagId = (await fabrik.SpeichernAsync(bezahlt)).Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- Grundmenge, Anmeldung, Rechte ----------

    [Fact]
    public async Task Anonym_bekommt_401()
    {
        var client = fabrik.ErzeugeAnonymenClient();

        (await client.GetAsync(ApiRouten.Belege.Liste)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync(ApiRouten.Belege.Zaehler)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync(Route(ApiRouten.Belege.Einzeln, Guid.NewGuid()))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.GetAsync(ApiRouten.Ansichten.Liste)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NurLesen_darf_alles_Lesende()
    {
        var beleg = await BelegAsync(b => b.Volltext = "Toner schwarz");
        var datei = await DateiAsync(beleg.Id, DateiRolle.Original, "2026/a.pdf", "%PDF-1.7 inhalt", "application/pdf");
        await DateiAsync(beleg.Id, DateiRolle.Visualisierung, $"visualisierung/{beleg.Id}.html", "<p>Hallo</p>", "text/html");
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        foreach (var route in new[]
        {
            ApiRouten.Belege.Liste,
            ApiRouten.Belege.Zaehler,
            Route(ApiRouten.Belege.Einzeln, beleg.Id),
            Route(ApiRouten.Belege.Nachbarn, beleg.Id),
            Route(ApiRouten.Belege.Aehnliche, beleg.Id),
            DateiRoute(beleg.Id, datei.Id),
            Route(ApiRouten.Belege.Visualisierung, beleg.Id),
            ApiRouten.Ansichten.Liste,
        })
        {
            (await client.GetAsync(route)).StatusCode.ShouldBe(HttpStatusCode.OK, route);
        }

        var angelegt = await client.PostAsJsonAsync(ApiRouten.Ansichten.Liste, Ansicht("Meine"));
        angelegt.StatusCode.ShouldBe(HttpStatusCode.Created);
        var ansicht = (await angelegt.Content.ReadFromJsonAsync<GespeicherteAnsichtDto>()).ShouldNotBeNull();
        (await client.DeleteAsync(Route(ApiRouten.Ansichten.Einzeln, ansicht.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Liste_zeigt_nur_fertige_und_fehlerhafte_Belege()
    {
        var fertig = await BelegAsync();
        var fehler = await BelegAsync(b => b.Status = BelegStatus.Fehler);
        await BelegAsync(b => b.Status = BelegStatus.Importiert);
        await BelegAsync(b => b.Status = BelegStatus.InVerarbeitung);

        var liste = await ListeAsync("");

        Ids(liste).ShouldBe([fertig.Id, fehler.Id], ignoreOrder: true);
        liste.Gesamt.ShouldBe(2);
    }

    // ---------- Volltext ----------

    [Fact]
    public async Task Volltext_findet_Begriff_im_Inhalt()
    {
        var toner = await BelegAsync(b => b.Volltext = "Lieferung\nToner schwarz für Laserdrucker");
        await BelegAsync(b => b.Volltext = "Büromaterial Kugelschreiber");

        Ids(await ListeAsync("suche=Toner")).ShouldBe([toner.Id]);
        Ids(await ListeAsync("suche=toner%20laserdrucker")).ShouldBe([toner.Id]);
    }

    [Fact]
    public async Task Volltext_sucht_das_letzte_Wort_als_Praefix()
    {
        var toner = await BelegAsync(b => b.Volltext = "Tonerkartusche schwarz");
        await BelegAsync(b => b.Volltext = "Papier");

        Ids(await ListeAsync("suche=Tonerkart")).ShouldBe([toner.Id]);
    }

    [Fact]
    public async Task Volltext_findet_Teil_der_Rechnungsnummer()
    {
        var treffer = await BelegAsync(b => b.Rechnungsnummer = "2026-48817");
        await BelegAsync(b => b.Rechnungsnummer = "2026-11111");

        Ids(await ListeAsync("suche=48817")).ShouldBe([treffer.Id]);
        Ids(await ListeAsync("suche=6-488")).ShouldBe([treffer.Id]);
    }

    [Fact]
    public async Task Volltext_findet_Korrespondentenname()
    {
        var kraemer = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Bürowelt Krämer KG"));
        var treffer = await BelegAsync(b => b.KorrespondentId = kraemer.Id);
        await BelegAsync();

        Ids(await ListeAsync("suche=Krämer")).ShouldBe([treffer.Id]);
    }

    [Fact]
    public async Task Suchfeld_Rechnungsnummer_sucht_nur_in_der_Rechnungsnummer()
    {
        var nummer = await BelegAsync(b => b.Rechnungsnummer = "2026-48817");
        await BelegAsync(b => b.Volltext = "Bezug auf Angebot 48817");

        Ids(await ListeAsync("suche=48817&suchfeld=TitelInhalt")).Count.ShouldBe(2);
        Ids(await ListeAsync("suche=48817&suchfeld=Rechnungsnummer")).ShouldBe([nummer.Id]);
    }

    // ---------- Filter ----------

    [Fact]
    public async Task Filter_TagIds_verlangt_alle_Tags()
    {
        var a = await fabrik.SpeichernAsync(Testdaten.Tag());
        var b = await fabrik.SpeichernAsync(Testdaten.Tag());
        var beide = await BelegAsync(tagIds: [a.Id, b.Id]);
        await BelegAsync(tagIds: [a.Id]);
        await BelegAsync();

        Ids(await ListeAsync($"tagIds={a.Id}&tagIds={b.Id}")).ShouldBe([beide.Id]);
        Ids(await ListeAsync($"tagIds={a.Id},{b.Id}")).ShouldBe([beide.Id]);
        Ids(await ListeAsync($"tagIds={a.Id}")).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Filter_KorrespondentIds()
    {
        var k1 = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var k2 = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var b1 = await BelegAsync(b => b.KorrespondentId = k1.Id);
        var b2 = await BelegAsync(b => b.KorrespondentId = k2.Id);
        await BelegAsync();

        Ids(await ListeAsync($"korrespondentIds={k1.Id}")).ShouldBe([b1.Id]);
        Ids(await ListeAsync($"korrespondentIds={k1.Id}&korrespondentIds={k2.Id}")).ShouldBe([b1.Id, b2.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Filter_KategorieIds()
    {
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var treffer = await BelegAsync(b => b.KategorieId = kategorie.Id);
        await BelegAsync();

        Ids(await ListeAsync($"kategorieIds={kategorie.Id}")).ShouldBe([treffer.Id]);
    }

    [Fact]
    public async Task Filter_Von_und_Bis_auf_Rechnungsdatum_einschliesslich()
    {
        await BelegAsync(b => b.Rechnungsdatum = new DateOnly(2025, 12, 31));
        var anfang = await BelegAsync(b => b.Rechnungsdatum = new DateOnly(2026, 1, 1));
        var ende = await BelegAsync(b => b.Rechnungsdatum = new DateOnly(2026, 1, 31));
        await BelegAsync(b => b.Rechnungsdatum = new DateOnly(2026, 2, 1));

        Ids(await ListeAsync("von=2026-01-01&bis=2026-01-31")).ShouldBe([anfang.Id, ende.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Filter_Betrag_auf_Brutto_oder_Netto()
    {
        var klein = await BelegAsync(b => (b.Netto, b.Brutto) = (50m, 59.50m));
        var gross = await BelegAsync(b => (b.Netto, b.Brutto) = (100m, 119m));

        Ids(await ListeAsync("betragVon=100&betragBis=120")).ShouldBe([gross.Id]);
        Ids(await ListeAsync("betragVon=100&betragArt=Netto")).ShouldBe([gross.Id]);
        Ids(await ListeAsync("betragVon=55&betragBis=60&betragArt=Brutto")).ShouldBe([klein.Id]);
        Ids(await ListeAsync("betragBis=55&betragArt=Netto")).ShouldBe([klein.Id]);
    }

    [Fact]
    public async Task Filter_Zahlstatus()
    {
        var bezahlt = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 1, 1), tagIds: [_zahlungsTagId]);
        var offen = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 2, 1));
        var ueberfaellig = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 1, 14));

        Ids(await ListeAsync("zahlstatus=Bezahlt")).ShouldBe([bezahlt.Id]);
        Ids(await ListeAsync("zahlstatus=Offen")).ShouldBe([offen.Id, ueberfaellig.Id], ignoreOrder: true);
        Ids(await ListeAsync("zahlstatus=Ueberfaellig")).ShouldBe([ueberfaellig.Id]);
    }

    [Fact]
    public async Task Filter_Quellen()
    {
        var ordner = await BelegAsync(b => b.Quelle = ImportQuelle.Ordner);
        var mail = await BelegAsync(b => b.Quelle = ImportQuelle.Mail);
        await BelegAsync(b => b.Quelle = ImportQuelle.Upload);

        Ids(await ListeAsync("quellen=Ordner&quellen=Mail")).ShouldBe([ordner.Id, mail.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Filter_DublettenAusschliessen()
    {
        var original = await BelegAsync();
        await BelegAsync(b => b.DuplikatVonId = original.Id);

        (await ListeAsync("")).Gesamt.ShouldBe(2);
        Ids(await ListeAsync("dublettenAusschliessen=true")).ShouldBe([original.Id]);
    }

    [Fact]
    public async Task Ungueltige_Parameter_liefern_400()
    {
        var client = fabrik.ErzeugeClient();

        foreach (var query in new[] { "seitengroesse=0", "seitengroesse=201", "seite=0", "zahlstatus=Egal", "tagIds=abc", "von=15.01.2026", "betragVon=zehn", "sortierung=1" })
        {
            (await client.GetAsync($"{ApiRouten.Belege.Liste}?{query}")).StatusCode.ShouldBe(HttpStatusCode.BadRequest, query);
        }

        (await client.GetAsync($"{ApiRouten.Belege.Liste}?seitengroesse=200")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------- Ansichten, Zahlstatus, Dubletten, Zähler ----------

    [Fact]
    public async Task Ansichten_Inbox_ZuPruefen_Offen_Ueberfaellig_mit_festem_Heute()
    {
        var inbox = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 2, 1), tagIds: [SystemIds.TagInbox]);
        var zuPruefen = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 1, 15), tagIds: [SystemIds.TagZuPruefen]);
        var ueberfaellig = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 1, 14));
        var bezahlt = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 1, 1), tagIds: [_zahlungsTagId]);

        Ids(await ListeAsync("ansicht=Alle")).Count.ShouldBe(4);
        Ids(await ListeAsync("ansicht=Inbox")).ShouldBe([inbox.Id]);
        Ids(await ListeAsync("ansicht=ZuPruefen")).ShouldBe([zuPruefen.Id]);
        Ids(await ListeAsync("ansicht=Offen")).ShouldBe([inbox.Id, zuPruefen.Id, ueberfaellig.Id], ignoreOrder: true);
        Ids(await ListeAsync("ansicht=Ueberfaellig")).ShouldBe([ueberfaellig.Id]);

        // Einen Tag später ist auch der am 15.01. fällige Beleg überfällig.
        fabrik.Zeit.Setze(Jetzt.AddDays(1));
        Ids(await ListeAsync("ansicht=Ueberfaellig")).ShouldBe([zuPruefen.Id, ueberfaellig.Id], ignoreOrder: true);
        (await ListeAsync("")).Eintraege.Single(e => e.Id == bezahlt.Id).Ueberfaellig.ShouldBeFalse();
    }

    [Fact]
    public async Task Zahlungs_Tag_macht_ueberfaelligen_Beleg_bezahlt()
    {
        var beleg = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 1, 1));
        (await ListeAsync("")).Eintraege.Single().Ueberfaellig.ShouldBeTrue();
        (await ZaehlerAsync()).Ueberfaellig.ShouldBe(1);

        await fabrik.SpeichernAsync(new BelegTag { BelegId = beleg.Id, TagId = _zahlungsTagId, GesetztAm = Jetzt });

        (await ListeAsync("")).Eintraege.Single().Ueberfaellig.ShouldBeFalse();
        Ids(await ListeAsync("ansicht=Ueberfaellig")).ShouldBeEmpty();
        Ids(await ListeAsync("zahlstatus=Bezahlt")).ShouldBe([beleg.Id]);
        var zaehler = await ZaehlerAsync();
        zaehler.Ueberfaellig.ShouldBe(0);
        zaehler.Offen.ShouldBe(0);
    }

    [Fact]
    public async Task Zaehler_in_einer_Antwort_ohne_Dubletten()
    {
        var original = await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 1, 1), tagIds: [SystemIds.TagInbox, SystemIds.TagZuPruefen]);
        await BelegAsync(b => (b.Faelligkeitsdatum, b.DuplikatVonId) = (new DateOnly(2026, 1, 1), original.Id), tagIds: [SystemIds.TagInbox, SystemIds.TagZuPruefen]);
        await BelegAsync(b => b.Faelligkeitsdatum = new DateOnly(2026, 3, 1));
        await BelegAsync(tagIds: [_zahlungsTagId]);
        await BelegAsync(b => b.Status = BelegStatus.InVerarbeitung, tagIds: [SystemIds.TagInbox]);

        (await ZaehlerAsync()).ShouldBe(new BelegZaehlerDto(Alle: 3, Inbox: 1, ZuPruefen: 1, Offen: 2, Ueberfaellig: 1));
    }

    [Fact]
    public async Task Leere_Datenbank_liefert_Zaehler_null()
    {
        (await ZaehlerAsync()).ShouldBe(new BelegZaehlerDto(0, 0, 0, 0, 0));
    }

    [Fact]
    public async Task Dubletten_fehlen_in_Summe_Offen_und_Ueberfaellig()
    {
        var original = await BelegAsync(b => (b.Brutto, b.Faelligkeitsdatum) = (100m, new DateOnly(2026, 1, 1)));
        var dublette = await BelegAsync(b => (b.Brutto, b.Faelligkeitsdatum, b.DuplikatVonId) = (100m, new DateOnly(2026, 1, 1), original.Id));

        var liste = await ListeAsync("");
        liste.Gesamt.ShouldBe(2);
        liste.SummeBrutto.ShouldBe(100m);
        var eintrag = liste.Eintraege.Single(e => e.Id == dublette.Id);
        eintrag.IstDublette.ShouldBeTrue();
        eintrag.Ueberfaellig.ShouldBeFalse();
        Ids(await ListeAsync("ansicht=Offen")).ShouldBe([original.Id]);
        Ids(await ListeAsync("zahlstatus=Ueberfaellig")).ShouldBe([original.Id]);
    }

    // ---------- Sortierung, Blättern, Summe ----------

    [Fact]
    public async Task Sortierung_mit_Gleichstand_nach_ErstelltAm_absteigend()
    {
        var datum = new DateOnly(2026, 1, 10);
        var alt = await BelegAsync(b => (b.Rechnungsdatum, b.ErstelltAm, b.Brutto) = (datum, Jetzt.AddDays(-3), 30m));
        var neu = await BelegAsync(b => (b.Rechnungsdatum, b.ErstelltAm, b.Brutto) = (datum, Jetzt.AddDays(-1), 10m));
        var spaet = await BelegAsync(b => (b.Rechnungsdatum, b.ErstelltAm, b.Brutto) = (new DateOnly(2026, 1, 12), Jetzt.AddDays(-2), 20m));

        Ids(await ListeAsync("")).ShouldBe([spaet.Id, neu.Id, alt.Id]);
        Ids(await ListeAsync("absteigend=false")).ShouldBe([neu.Id, alt.Id, spaet.Id]);
        Ids(await ListeAsync("sortierung=Brutto&absteigend=false")).ShouldBe([neu.Id, spaet.Id, alt.Id]);
        Ids(await ListeAsync("sortierung=ErstelltAm")).ShouldBe([neu.Id, spaet.Id, alt.Id]);
    }

    [Fact]
    public async Task Sortierung_nach_Korrespondent_und_Faelligkeit()
    {
        var anton = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Anton GmbH"));
        var berta = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Berta AG"));
        var b1 = await BelegAsync(b => (b.KorrespondentId, b.Faelligkeitsdatum) = (berta.Id, new DateOnly(2026, 1, 20)));
        var b2 = await BelegAsync(b => (b.KorrespondentId, b.Faelligkeitsdatum) = (anton.Id, new DateOnly(2026, 1, 25)));

        Ids(await ListeAsync("sortierung=Korrespondent&absteigend=false")).ShouldBe([b2.Id, b1.Id]);
        Ids(await ListeAsync("sortierung=Faelligkeit&absteigend=false")).ShouldBe([b1.Id, b2.Id]);
    }

    [Fact]
    public async Task Blaettern_ist_stabil_und_Summe_gilt_fuer_alle_Seiten()
    {
        List<Guid> alle = [];
        for (var i = 0; i < 7; i++)
        {
            // Gleiches Rechnungsdatum und gleiche Erstellzeit: nur die ID entscheidet.
            alle.Add((await BelegAsync(b => b.Brutto = 10m)).Id);
        }

        var seiten = new List<BelegListeDto>();
        for (var seite = 1; seite <= 3; seite++)
        {
            seiten.Add(await ListeAsync($"seite={seite}&seitengroesse=3"));
        }

        var gesehen = seiten.SelectMany(Ids).ToList();
        gesehen.ShouldBe(alle, ignoreOrder: true);
        gesehen.Distinct().Count().ShouldBe(7);
        seiten.Select(s => s.Eintraege.Count).ShouldBe([3, 3, 1]);
        seiten.ShouldAllBe(s => s.Gesamt == 7 && s.SummeBrutto == 70m && s.Seitengroesse == 3);
        (await ListeAsync("seite=1&seitengroesse=3")).Eintraege.Select(e => e.Id).ShouldBe(Ids(seiten[0]));
        (await ListeAsync("")).Seitengroesse.ShouldBe(50);
    }

    // ---------- Listeneintrag ----------

    [Fact]
    public async Task Listeneintrag_mit_Format_Auszug_Vorschau_und_Logo()
    {
        var mitLogo = Testdaten.Korrespondent("bürowelt Krämer KG");
        mitLogo.LogoPfad = "logos/kraemer.png";
        mitLogo.LogoQuelle = LogoQuelle.Upload;
        await fabrik.SpeichernAsync(mitLogo);
        var ohneLogo = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Stadtwerke"));

        var scan = await BelegAsync(b =>
        {
            b.Quelle = ImportQuelle.Ordner;
            b.KorrespondentId = mitLogo.Id;
            b.Volltext = "Zeile eins\r\nZeile zwei\n\nZeile drei" + new string('x', 400);
            b.Titel = "Toner";
        });
        await DateiAsync(scan.Id, DateiRolle.Vorschau, $"vorschau/{scan.Id}.png", "png", "image/png");
        var xrechnung = await BelegAsync(b => (b.ExtraktionsQuelle, b.KorrespondentId) = (ExtraktionsQuelle.XRechnung, ohneLogo.Id));
        var zugferd = await BelegAsync(b => b.ExtraktionsQuelle = ExtraktionsQuelle.ZUGFeRD);
        var pdf = await BelegAsync(b => (b.ExtraktionsQuelle, b.Titel) = (ExtraktionsQuelle.DocumentIntelligence, null));

        var eintraege = (await ListeAsync("")).Eintraege.ToDictionary(e => e.Id);

        var s = eintraege[scan.Id];
        s.Format.ShouldBe("Scan");
        s.Titel.ShouldBe("Toner");
        s.Auszug.ShouldStartWith("Zeile eins · Zeile zwei · Zeile drei");
        s.Auszug.Length.ShouldBe("Zeile eins · Zeile zwei · Zeile drei".Length + 300 - "Zeile eins\r\nZeile zwei\n\nZeile drei".Length);
        s.VorschauUrl.ShouldBe($"/api/belege/{scan.Id}/vorschau");
        s.Logo.ShouldBe(new Contracts.Dto.LogoDto($"/api/korrespondenten/{mitLogo.Id}/logo", "BK", LogoQuelle.Upload));
        s.Korrespondent.ShouldNotBeNull().Name.ShouldBe("bürowelt Krämer KG");

        eintraege[xrechnung.Id].Format.ShouldBe("XRechnung");
        eintraege[xrechnung.Id].Logo.ShouldBe(new Contracts.Dto.LogoDto(null, "S", LogoQuelle.Keine));
        eintraege[xrechnung.Id].VorschauUrl.ShouldBeNull();
        eintraege[zugferd.Id].Format.ShouldBe("ZUGFeRD");
        eintraege[pdf.Id].Format.ShouldBe("PDF");
        eintraege[pdf.Id].Titel.ShouldBe(pdf.Rechnungsnummer);
        eintraege[pdf.Id].Logo.Initialen.ShouldBe("?");
    }

    // ---------- Detail ----------

    [Fact]
    public async Task Detail_ist_vollstaendig()
    {
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie("Bürobedarf"));
        var korrespondent = Testdaten.Korrespondent("Bürowelt Krämer KG");
        korrespondent.DefaultKategorieId = kategorie.Id;
        await fabrik.SpeichernAsync(korrespondent);
        var artikel = await fabrik.SpeichernAsync(new Artikel
        {
            Id = Guid.NewGuid(),
            KorrespondentId = korrespondent.Id,
            Name = "Toner schwarz",
            NameNormalisiert = "toner schwarz",
        });

        var beleg = await BelegAsync(b =>
        {
            b.KorrespondentId = korrespondent.Id;
            b.KategorieId = kategorie.Id;
            b.Rechnungsnummer = "2026-48817";
            b.Rechnungsdatum = new DateOnly(2026, 1, 10);
            b.LeistungVon = new DateOnly(2026, 1, 1);
            b.LeistungBis = new DateOnly(2026, 1, 31);
            b.ExtraktionsQuelle = ExtraktionsQuelle.XRechnung;
            b.ExtrahiertAm = Jetzt;
            b.DiKonfidenz = 0.9m;
            b.Seitenanzahl = 2;
            b.Steuerzeilen =
            [
                new Steuerzeile { Id = Guid.NewGuid(), Satz = 19m, Netto = 100m, Steuer = 19.01m },
                new Steuerzeile { Id = Guid.NewGuid(), Satz = 7m, Netto = 100m, Steuer = 9m },
            ];
            b.Positionen =
            [
                new Position { Id = Guid.NewGuid(), Nr = 2, Text = "Papier", Gesamt = 100m },
                new Position { Id = Guid.NewGuid(), Nr = 1, Text = "Toner schwarz", Menge = 2, Einheit = "Stück", Einzelpreis = 50m, Gesamt = 100m, Steuersatz = 19m, ArtikelId = artikel.Id },
            ];
            b.Befunde =
            [
                new PruefBefund { Id = Guid.NewGuid(), Pruefung = PruefungArt.Pflichtfelder, Ergebnis = PruefErgebnis.Ok, Detail = "alles da", Zeitpunkt = Jetzt },
                new PruefBefund { Id = Guid.NewGuid(), Pruefung = PruefungArt.Positionssumme, Ergebnis = PruefErgebnis.Fehlgeschlagen, Detail = "Differenz 2,00 €", Zeitpunkt = Jetzt },
            ];
        });
        var original = await DateiAsync(beleg.Id, DateiRolle.Original, "2026/kraemer/rechnung.xml", "<Invoice/>", "application/xml", pruefung: true);
        var teil = await BelegAsync(b => b.AufgeteiltAusBelegId = beleg.Id);

        var antwort = await fabrik.ErzeugeClient().GetAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id));
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await antwort.Content.ReadFromJsonAsync<BelegDetailDto>()).ShouldNotBeNull();

        detail.Rechnungsnummer.ShouldBe("2026-48817");
        detail.Korrespondent.ShouldBe(new Contracts.Dto.VerweisDto(korrespondent.Id, "Bürowelt Krämer KG"));
        detail.Kategorie.ShouldBe(new Contracts.Dto.VerweisDto(kategorie.Id, "Bürobedarf"));
        detail.DefaultKategorie.ShouldBe(new Contracts.Dto.VerweisDto(kategorie.Id, "Bürobedarf"));
        detail.Status.ShouldBe(BelegStatus.Fertig);
        detail.Belegart.ShouldBe(Belegart.Rechnung);
        detail.LeistungVon.ShouldBe(new DateOnly(2026, 1, 1));
        detail.LeistungBis.ShouldBe(new DateOnly(2026, 1, 31));
        detail.Format.ShouldBe("XRechnung");
        detail.IstERechnung.ShouldBeTrue();
        detail.Extraktion.ShouldBe(new ExtraktionDto(ExtraktionsQuelle.XRechnung, 0.9m, Jetzt, 2));
        detail.LoeschsperreBis.ShouldBe(new DateOnly(2034, 12, 31));

        detail.Steuerzeilen.ShouldBe(
        [
            new SteuerzeileDto(19m, 100m, 19.01m, true),
            new SteuerzeileDto(7m, 100m, 9m, false),
        ]);

        detail.Positionen.Select(p => p.Nr).ShouldBe([1, 2]);
        var position = detail.Positionen[0];
        position.Artikel.ShouldBe(new Contracts.Dto.VerweisDto(artikel.Id, "Toner schwarz"));
        position.Menge.ShouldBe(2m);
        position.Einzelpreis.ShouldBe(50m);
        position.Gesamt.ShouldBe(100m);

        detail.Befunde.Select(f => (f.Pruefung, f.Name, f.Ergebnis)).ShouldBe(
        [
            (PruefungArt.Positionssumme, "Summe der Positionen = Netto", PruefErgebnis.Fehlgeschlagen),
            (PruefungArt.Pflichtfelder, "Pflichtfelder", PruefErgebnis.Ok),
        ]);

        var datei = detail.Dateien.ShouldHaveSingleItem();
        datei.Id.ShouldBe(original.Id);
        datei.Rolle.ShouldBe(DateiRolle.Original);
        datei.PruefsummeOk.ShouldBe(true);
        datei.Url.ShouldBe($"/api/belege/{beleg.Id}/dateien/{original.Id}");

        detail.Teilbelege.ShouldBe([teil.Id]);
        detail.AufgeteiltAusBelegId.ShouldBeNull();
        detail.Dubletten.ShouldBe(new DubletteDto(null, [], false, false), new DubletteVergleich());
    }

    [Fact]
    public async Task Detail_zeigt_Dubletten_Hash_und_fachlichen_Treffer()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var original = await BelegAsync(b => (b.KorrespondentId, b.Rechnungsnummer, b.Brutto, b.Titel) = (korrespondent.Id, "RE 2026/1", 119m, "Original"));
        var fachlich = await BelegAsync(b =>
            (b.KorrespondentId, b.Rechnungsnummer, b.Brutto, b.DuplikatVonId, b.ErstelltAm) = (korrespondent.Id, "RE-2026-1", 119m, original.Id, Jetzt.AddHours(1)));
        var hash = await BelegAsync(b => (b.DuplikatVonId, b.ErstelltAm) = (original.Id, Jetzt.AddHours(2)));
        await DateiAsync(original.Id, DateiRolle.Original, "2026/o.pdf", "gleich", "application/pdf");
        await DateiAsync(hash.Id, DateiRolle.Original, "2026/h.pdf", "gleich", "application/pdf");

        var detailOriginal = await DetailAsync(original.Id);
        detailOriginal.IstDublette.ShouldBeFalse();
        detailOriginal.Dubletten.DuplikatVon.ShouldBeNull();
        detailOriginal.Dubletten.Duplikate.Select(d => d.Id).ShouldBe([fachlich.Id, hash.Id]);
        detailOriginal.Dubletten.HashTreffer.ShouldBeTrue();
        detailOriginal.Dubletten.FachlicherTreffer.ShouldBeTrue();

        var detailFachlich = await DetailAsync(fachlich.Id);
        detailFachlich.IstDublette.ShouldBeTrue();
        detailFachlich.Dubletten.DuplikatVon.ShouldBe(new Contracts.Dto.VerweisDto(original.Id, "Original"));
        detailFachlich.Dubletten.HashTreffer.ShouldBeFalse();
        detailFachlich.Dubletten.FachlicherTreffer.ShouldBeTrue();

        var detailHash = await DetailAsync(hash.Id);
        detailHash.Dubletten.HashTreffer.ShouldBeTrue();
        detailHash.Dubletten.FachlicherTreffer.ShouldBeFalse();
    }

    [Fact]
    public async Task Unbekannte_ID_liefert_404()
    {
        var client = fabrik.ErzeugeClient();
        var id = Guid.NewGuid();

        foreach (var route in new[]
        {
            Route(ApiRouten.Belege.Einzeln, id),
            Route(ApiRouten.Belege.Nachbarn, id),
            Route(ApiRouten.Belege.Aehnliche, id),
            DateiRoute(id, Guid.NewGuid()),
            Route(ApiRouten.Belege.Visualisierung, id),
        })
        {
            var antwort = await client.GetAsync(route);
            antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound, route);
            antwort.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        }

        (await client.DeleteAsync(Route(ApiRouten.Ansichten.Einzeln, id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- Nachbarn, Ähnliche ----------

    [Fact]
    public async Task Nachbarn_am_Anfang_in_der_Mitte_und_am_Ende()
    {
        var erster = await BelegAsync(b => b.Rechnungsdatum = new DateOnly(2026, 1, 12), tagIds: [SystemIds.TagInbox]);
        var zweiter = await BelegAsync(b => b.Rechnungsdatum = new DateOnly(2026, 1, 11), tagIds: [SystemIds.TagInbox]);
        var dritter = await BelegAsync(b => b.Rechnungsdatum = new DateOnly(2026, 1, 10), tagIds: [SystemIds.TagInbox]);
        var ausserhalb = await BelegAsync(b => b.Rechnungsdatum = new DateOnly(2026, 1, 13));

        (await NachbarnAsync(erster.Id, "ansicht=Inbox")).ShouldBe(new BelegNachbarnDto(null, zweiter.Id, 1, 3));
        (await NachbarnAsync(zweiter.Id, "ansicht=Inbox")).ShouldBe(new BelegNachbarnDto(erster.Id, dritter.Id, 2, 3));
        (await NachbarnAsync(dritter.Id, "ansicht=Inbox")).ShouldBe(new BelegNachbarnDto(zweiter.Id, null, 3, 3));
        (await NachbarnAsync(dritter.Id, "ansicht=Inbox&absteigend=false")).ShouldBe(new BelegNachbarnDto(null, zweiter.Id, 1, 3));
        (await NachbarnAsync(ausserhalb.Id, "ansicht=Inbox")).ShouldBe(new BelegNachbarnDto(null, null, 0, 3));
        (await NachbarnAsync(ausserhalb.Id, "")).ShouldBe(new BelegNachbarnDto(null, erster.Id, 1, 4));
    }

    [Fact]
    public async Task Aehnliche_nach_Korrespondent_und_Volltext_ohne_den_Beleg_selbst()
    {
        var k1 = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var k2 = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var beleg = await BelegAsync(b => (b.KorrespondentId, b.Volltext) = (k1.Id, "Toner Patrone Laserdrucker Toner Patrone"));
        var gleicherKorrespondent = await BelegAsync(b => (b.KorrespondentId, b.Volltext) = (k1.Id, "Miete Januar"));
        var gleicherInhalt = await BelegAsync(b => (b.KorrespondentId, b.Volltext) = (k2.Id, "Toner Patrone für Laserdrucker"));
        await BelegAsync(b => (b.KorrespondentId, b.Volltext, b.Titel, b.Rechnungsnummer) = (k2.Id, "Strom Abschlag", "Strom", "S-1"));

        var client = fabrik.ErzeugeClient();
        var aehnliche = (await client.GetFromJsonAsync<BelegListeneintragDto[]>(Route(ApiRouten.Belege.Aehnliche, beleg.Id))).ShouldNotBeNull();

        aehnliche.Select(a => a.Id).ShouldBe([gleicherKorrespondent.Id, gleicherInhalt.Id], ignoreOrder: true);

        var nurEiner = (await client.GetFromJsonAsync<BelegListeneintragDto[]>($"{Route(ApiRouten.Belege.Aehnliche, beleg.Id)}?anzahl=1")).ShouldNotBeNull();
        nurEiner.Length.ShouldBe(1);
        (await client.GetAsync($"{Route(ApiRouten.Belege.Aehnliche, beleg.Id)}?anzahl=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------- Dateien, Visualisierung ----------

    [Fact]
    public async Task Datei_Download_mit_Typ_Inhalt_und_Dateiname()
    {
        var beleg = await BelegAsync();
        var datei = await DateiAsync(beleg.Id, DateiRolle.Original, "2026/lieferant/2026-01-10_RE-1.pdf", "%PDF-1.7 Inhalt", "application/pdf");

        var antwort = await fabrik.ErzeugeClient().GetAsync(DateiRoute(beleg.Id, datei.Id));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        antwort.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");
        antwort.Content.Headers.ContentDisposition?.FileName?.Trim('"').ShouldBe("2026-01-10_RE-1.pdf");
        (await antwort.Content.ReadAsStringAsync()).ShouldBe("%PDF-1.7 Inhalt");
    }

    [Fact]
    public async Task Datei_inline_und_mit_Range()
    {
        var beleg = await BelegAsync();
        var datei = await DateiAsync(beleg.Id, DateiRolle.Visualisierung, $"visualisierung/{beleg.Id}.html", "0123456789", "text/html");
        var client = fabrik.ErzeugeClient();

        var inline = await client.GetAsync($"{DateiRoute(beleg.Id, datei.Id)}?inline=true");
        inline.StatusCode.ShouldBe(HttpStatusCode.OK);
        inline.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
        inline.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        using var anfrage = new HttpRequestMessage(HttpMethod.Get, DateiRoute(beleg.Id, datei.Id));
        anfrage.Headers.Range = new RangeHeaderValue(2, 5);
        var teil = await client.SendAsync(anfrage);
        teil.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        (await teil.Content.ReadAsStringAsync()).ShouldBe("2345");
        teil.Content.Headers.ContentRange?.Length.ShouldBe(10);
    }

    [Fact]
    public async Task Pfad_Ausbruch_und_Datei_eines_anderen_Belegs_liefern_404()
    {
        var beleg = await BelegAsync();
        var anderer = await BelegAsync();
        await File.WriteAllTextAsync(Path.Combine(fabrik.TempOrdner, "geheim.txt"), "geheim");
        var ausbruch = await DateiAsync(beleg.Id, DateiRolle.Original, "../geheim.txt", null, "text/plain");
        var fremd = await DateiAsync(anderer.Id, DateiRolle.Original, "2026/fremd.pdf", "fremd", "application/pdf");
        var fehlt = await DateiAsync(beleg.Id, DateiRolle.Original, "2026/gibt-es-nicht.pdf", null, "application/pdf");
        var client = fabrik.ErzeugeClient();

        (await client.GetAsync(DateiRoute(beleg.Id, ausbruch.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync(DateiRoute(beleg.Id, fremd.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync(DateiRoute(beleg.Id, fehlt.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Visualisierung_mit_CSP_Header_und_404_ohne()
    {
        var beleg = await BelegAsync();
        var ohne = await BelegAsync();
        await DateiAsync(beleg.Id, DateiRolle.Visualisierung, $"visualisierung/{beleg.Id}.html", "<html><body>Rechnung</body></html>", "text/html");
        var client = fabrik.ErzeugeClient();

        var antwort = await client.GetAsync(Route(ApiRouten.Belege.Visualisierung, beleg.Id));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        antwort.Headers.GetValues("Content-Security-Policy").ShouldHaveSingleItem()
            .ShouldBe("sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data:");
        (await antwort.Content.ReadAsStringAsync()).ShouldBe("<html><body>Rechnung</body></html>");
        (await client.GetAsync(Route(ApiRouten.Belege.Visualisierung, ohne.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- Gespeicherte Ansichten ----------

    [Fact]
    public async Task Ansichten_sind_je_Benutzer_getrennt_und_gleicher_Name_ueberschreibt()
    {
        var admin = fabrik.ErzeugeClient(Rolle.Admin);
        var benutzer = fabrik.ErzeugeClient(Rolle.Benutzer);
        var filter = new BelegFilterDto { Ansicht = BelegAnsicht.Ueberfaellig, Suche = "Toner", TagIds = [Guid.NewGuid()], Seitengroesse = 25 };

        var erste = await AnlegenAsync(admin, new GespeicherteAnsichtAnlegenDto("Überfällig", filter, ["Datum", "Betrag"], "Tabelle"));
        await AnlegenAsync(benutzer, Ansicht("Überfällig"));
        var zweite = await AnlegenAsync(admin, new GespeicherteAnsichtAnlegenDto("Überfällig", filter with { Suche = "Papier" }, ["Datum"], "GrosseKarten"));

        zweite.Id.ShouldBe(erste.Id);
        var liste = (await admin.GetFromJsonAsync<GespeicherteAnsichtDto[]>(ApiRouten.Ansichten.Liste)).ShouldNotBeNull();
        var ansicht = liste.ShouldHaveSingleItem();
        ansicht.Name.ShouldBe("Überfällig");
        ansicht.Filter.ShouldBe(filter with { Suche = "Papier" }, new FilterVergleich());
        ansicht.Spalten.ShouldBe(["Datum"]);
        ansicht.Darstellung.ShouldBe("GrosseKarten");

        var fremde = (await benutzer.GetFromJsonAsync<GespeicherteAnsichtDto[]>(ApiRouten.Ansichten.Liste)).ShouldNotBeNull().ShouldHaveSingleItem();
        fremde.Id.ShouldNotBe(erste.Id);
        (await benutzer.DeleteAsync(Route(ApiRouten.Ansichten.Einzeln, erste.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.DeleteAsync(Route(ApiRouten.Ansichten.Einzeln, erste.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<GespeicherteAnsichtDto[]>(ApiRouten.Ansichten.Liste)).ShouldNotBeNull().ShouldBeEmpty();
        (await benutzer.GetFromJsonAsync<GespeicherteAnsichtDto[]>(ApiRouten.Ansichten.Liste)).ShouldNotBeNull().Length.ShouldBe(1);
    }

    [Fact]
    public async Task Ungueltige_Ansicht_liefert_400()
    {
        var client = fabrik.ErzeugeClient();

        (await client.PostAsJsonAsync(ApiRouten.Ansichten.Liste, Ansicht(" "))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(ApiRouten.Ansichten.Liste, Ansicht("X") with { Darstellung = "Kacheln" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------- Hilfen ----------

    private async Task<Beleg> BelegAsync(Action<Beleg>? anpassen = null, Guid[]? tagIds = null)
    {
        var beleg = Testdaten.Beleg();
        beleg.Faelligkeitsdatum = null;
        anpassen?.Invoke(beleg);
        beleg.Tags = [.. (tagIds ?? []).Select(t => new BelegTag { BelegId = beleg.Id, TagId = t, GesetztAm = Jetzt })];
        return await fabrik.SpeichernAsync(beleg);
    }

    /// <summary>Legt eine <see cref="BelegDatei"/> an und schreibt <paramref name="inhalt"/> unter die passende Wurzel.</summary>
    private async Task<BelegDatei> DateiAsync(
        Guid belegId, DateiRolle rolle, string pfad, string? inhalt, string mimeType, bool? pruefung = null)
    {
        var wurzel = Path.Combine(fabrik.TempOrdner, rolle is DateiRolle.Original or DateiRolle.EingebettetesXml ? "ablage" : "daten");
        var bytes = Encoding.UTF8.GetBytes(inhalt ?? "");
        if (inhalt is not null)
        {
            var absolut = Path.Combine(wurzel, pfad);
            Directory.CreateDirectory(Path.GetDirectoryName(absolut)!);
            await File.WriteAllBytesAsync(absolut, bytes);
        }

        return await fabrik.SpeichernAsync(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = belegId,
            Rolle = rolle,
            Pfad = pfad,
            Sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)),
            Groesse = bytes.Length,
            MimeType = mimeType,
            AbgelegtAm = Jetzt,
            LetztePruefungAm = pruefung is null ? null : Jetzt,
            PruefErgebnis = pruefung,
        });
    }

    private async Task<BelegListeDto> ListeAsync(string query) =>
        (await fabrik.ErzeugeClient().GetFromJsonAsync<BelegListeDto>($"{ApiRouten.Belege.Liste}?{query}")).ShouldNotBeNull();

    private async Task<BelegZaehlerDto> ZaehlerAsync() =>
        (await fabrik.ErzeugeClient().GetFromJsonAsync<BelegZaehlerDto>(ApiRouten.Belege.Zaehler)).ShouldNotBeNull();

    private async Task<BelegDetailDto> DetailAsync(Guid id) =>
        (await fabrik.ErzeugeClient().GetFromJsonAsync<BelegDetailDto>(Route(ApiRouten.Belege.Einzeln, id))).ShouldNotBeNull();

    private async Task<BelegNachbarnDto> NachbarnAsync(Guid id, string query) =>
        (await fabrik.ErzeugeClient().GetFromJsonAsync<BelegNachbarnDto>($"{Route(ApiRouten.Belege.Nachbarn, id)}?{query}")).ShouldNotBeNull();

    private static async Task<GespeicherteAnsichtDto> AnlegenAsync(HttpClient client, GespeicherteAnsichtAnlegenDto eingabe)
    {
        var antwort = await client.PostAsJsonAsync(ApiRouten.Ansichten.Liste, eingabe);
        antwort.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await antwort.Content.ReadFromJsonAsync<GespeicherteAnsichtDto>()).ShouldNotBeNull();
    }

    private static GespeicherteAnsichtAnlegenDto Ansicht(string name) => new(name, new BelegFilterDto(), [], "Tabelle");

    private static List<Guid> Ids(BelegListeDto liste) => [.. liste.Eintraege.Select(e => e.Id)];

    private static string Route(string vorlage, Guid id) => vorlage.Replace("{id}", id.ToString(), StringComparison.Ordinal);

    private static string DateiRoute(Guid belegId, Guid dateiId) =>
        Route(ApiRouten.Belege.Datei, belegId).Replace("{dateiId}", dateiId.ToString(), StringComparison.Ordinal);

    /// <summary>Vergleicht Filter inhaltlich (Arrays elementweise statt per Referenz).</summary>
    private sealed class FilterVergleich : IEqualityComparer<BelegFilterDto>
    {
        public bool Equals(BelegFilterDto? x, BelegFilterDto? y) =>
            x is not null && y is not null
            && x with { TagIds = [], KorrespondentIds = [], KategorieIds = [], Quellen = [] }
                == y with { TagIds = x.TagIds.Length == 0 ? [] : [], KorrespondentIds = [], KategorieIds = [], Quellen = [] }
                && x.TagIds.SequenceEqual(y.TagIds)
                && x.KorrespondentIds.SequenceEqual(y.KorrespondentIds)
                && x.KategorieIds.SequenceEqual(y.KategorieIds)
                && x.Quellen.SequenceEqual(y.Quellen);

        public int GetHashCode(BelegFilterDto obj) => 0;
    }

    private sealed class DubletteVergleich : IEqualityComparer<DubletteDto>
    {
        public bool Equals(DubletteDto? x, DubletteDto? y) =>
            x is not null && y is not null
            && x.DuplikatVon == y.DuplikatVon
            && x.Duplikate.Select(d => d.Id).SequenceEqual(y.Duplikate.Select(d => d.Id))
            && x.HashTreffer == y.HashTreffer
            && x.FachlicherTreffer == y.FachlicherTreffer;

        public int GetHashCode(DubletteDto obj) => 0;
    }
}
