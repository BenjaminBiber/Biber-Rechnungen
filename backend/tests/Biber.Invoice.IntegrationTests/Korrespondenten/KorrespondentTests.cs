using System.Net;
using System.Text.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Korrespondenten;

[Collection("Api")]
public sealed class KorrespondentTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private const string GueltigeIban = "DE89370400440532013000";
    private const string ZweiteIban = "GB82WEST12345698765432";

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Liste_filtert_nach_Typ_und_Kunden_sind_leer()
    {
        await fabrik.SpeichernAsync(Testdaten.Korrespondent("Bürobedarf Nord"));
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        var lieferanten = await ListeAsync(client, "?typ=Lieferanten");
        var standard = await ListeAsync(client, "");
        var kunden = await ListeAsync(client, "?typ=Kunden");
        var alle = await ListeAsync(client, "?typ=Alle");

        lieferanten.ShouldHaveSingleItem().Name.ShouldBe("Bürobedarf Nord");
        standard.ShouldHaveSingleItem();
        kunden.ShouldBeEmpty();
        alle.ShouldHaveSingleItem();
        (await client.GetAsync(ApiRouten.Korrespondenten.Liste + "?typ=Quatsch")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Suche_findet_Alias_UstIdNr_mit_Leerzeichen_und_Iban()
    {
        var k = Testdaten.Korrespondent("Telekom Deutschland GmbH", "DE123456789");
        k.Aliase = ["T-Mobile"];
        k.Ibans = [GueltigeIban];
        await fabrik.SpeichernAsync(k);
        await fabrik.SpeichernAsync(Testdaten.Korrespondent("Anderer Lieferant"));
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await ListeAsync(client, "?suche=mobile")).ShouldHaveSingleItem().Id.ShouldBe(k.Id);
        (await ListeAsync(client, "?suche=" + Uri.EscapeDataString("DE 123 456 789"))).ShouldHaveSingleItem().Id.ShouldBe(k.Id);
        (await ListeAsync(client, "?suche=" + Uri.EscapeDataString("de89 3704 0044"))).ShouldHaveSingleItem().Id.ShouldBe(k.Id);
        (await ListeAsync(client, "?suche=gibtsnicht")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Liste_summiert_brutto_im_Jahr_ohne_Dubletten_und_nur_EUR()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Summen AG"));
        var original = await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 2, 1), brutto: 100m, korrespondentId: k.Id));
        var dublette = Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 2, 1), brutto: 100m, korrespondentId: k.Id);
        dublette.DuplikatVonId = original.Id;
        await fabrik.SpeichernAsync(dublette);
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 3, 1), brutto: 50.50m, korrespondentId: k.Id));
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2025, 12, 31), brutto: 1000m, korrespondentId: k.Id));
        var usd = Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 4, 1), brutto: 70m, korrespondentId: k.Id);
        usd.Waehrung = "USD";
        await fabrik.SpeichernAsync(usd);
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        var eintrag = (await ListeAsync(client, "")).ShouldHaveSingleItem();
        var vorjahr = (await ListeAsync(client, "?jahr=2025")).ShouldHaveSingleItem();

        eintrag.KostenJahr.ShouldBe(150.50m);
        eintrag.AnzahlBelege.ShouldBe(4);
        eintrag.LetzterBeleg.ShouldBe(new DateOnly(2026, 4, 1));
        vorjahr.KostenJahr.ShouldBe(1000m);
    }

    [Fact]
    public async Task Liste_ist_nach_Kosten_sortiert_und_meldet_Dublette_und_neu()
    {
        var kraemer = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Bürowelt Krämer KG"));
        var neu = Testdaten.Korrespondent("Buerowelt Kraemer");
        neu.Geprueft = false;
        await fabrik.SpeichernAsync(neu);
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 1, 5), brutto: 10m, korrespondentId: kraemer.Id));
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 1, 5), brutto: 20m, korrespondentId: neu.Id));
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        var liste = await ListeAsync(client, "");

        liste.Select(e => e.Id).ShouldBe([neu.Id, kraemer.Id]);
        liste[0].IstNeu.ShouldBeTrue();
        liste[0].Hinweis.ShouldBe("noch nicht geprüft · vermutlich Dublette von ‚Bürowelt Krämer KG‘");
        liste[1].IstNeu.ShouldBeFalse();
        liste[1].Hinweis.ShouldBe("vermutlich Dublette von ‚Buerowelt Kraemer‘");
    }

    [Fact]
    public async Task Detail_liefert_Kennzahlen_gegen_Handrechnung()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Kennzahlen GmbH"));
        var bezahltTag = Testdaten.Tag("bezahlt");
        bezahltTag.IstZahlungsTag = true;
        await fabrik.SpeichernAsync(bezahltTag);

        var jan = Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 1, 10), brutto: 100m, korrespondentId: k.Id);
        jan.Faelligkeitsdatum = new DateOnly(2026, 1, 24);
        await fabrik.SpeichernAsync(jan);
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 3, 5), brutto: 200m, korrespondentId: k.Id));
        var bezahlt = Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 3, 20), brutto: 50m, korrespondentId: k.Id);
        bezahlt.Faelligkeitsdatum = new DateOnly(2026, 1, 1);
        bezahlt.Tags = [new BelegTag { TagId = bezahltTag.Id, GesetztAm = fabrik.Zeit.GetUtcNow() }];
        await fabrik.SpeichernAsync(bezahlt);
        var jul = Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 7, 1), brutto: 300m, korrespondentId: k.Id);
        jul.Faelligkeitsdatum = new DateOnly(2026, 7, 15);
        await fabrik.SpeichernAsync(jul);
        var dublette = Testdaten.Beleg(rechnungsdatum: new DateOnly(2026, 2, 1), brutto: 999m, korrespondentId: k.Id);
        dublette.DuplikatVonId = jan.Id;
        await fabrik.SpeichernAsync(dublette);
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2025, 2, 1), brutto: 100m, korrespondentId: k.Id));
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2025, 6, 1), brutto: 100m, korrespondentId: k.Id));
        await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsdatum: new DateOnly(2025, 12, 1), brutto: 400m, korrespondentId: k.Id));
        await fabrik.SpeichernAsync(new MatchingModell
        {
            Id = Guid.NewGuid(),
            Ziel = "Korrespondent",
            TrainiertAm = new DateTimeOffset(2026, 6, 1, 3, 0, 0, TimeSpan.Zero),
        });
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        var detail = (await client.GetJsonAsync<KorrespondentDetailDto>(Einzeln(k.Id))).ShouldNotBeNull();

        var z = detail.Kennzahlen;
        z.KostenJahr.ShouldBe(650m);
        z.DurchschnittJeBeleg.ShouldBe(162.50m);
        z.AenderungVorjahr.ShouldBe(0.75m); // bis 15.06.: 350 € gegenüber 200 € im Vorjahr
        z.AnzahlBelege.ShouldBe(7);
        z.Seit.ShouldBe(new DateOnly(2025, 2, 1));
        z.OffenAnzahl.ShouldBe(6);
        z.OffenSumme.ShouldBe(1200m);
        z.NaechsteFaelligkeit.ShouldBe(new DateOnly(2026, 1, 24));
        detail.KostenverlaufMonate.ShouldBe([100m, 0m, 250m, 0m, 0m, 0m, 300m, 0m, 0m, 0m, 0m, 0m]);
        detail.LetzteBelege.Length.ShouldBe(5);
        detail.LetzteBelege[0].Id.ShouldBe(jul.Id);
        detail.AutoTrainiertAm.ShouldBe(new DateTimeOffset(2026, 6, 1, 3, 0, 0, TimeSpan.Zero));

        var vorjahr = (await client.GetJsonAsync<KorrespondentDetailDto>(Einzeln(k.Id) + "?jahr=2025")).ShouldNotBeNull();
        vorjahr.Kennzahlen.KostenJahr.ShouldBe(600m);
        vorjahr.Kennzahlen.AenderungVorjahr.ShouldBeNull();
        (await client.GetAsync(Einzeln(Guid.NewGuid()))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Detail_listet_Artikel_mit_Kaeufen_und_Preisaenderung()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Artikel KG"));
        var papier = await fabrik.SpeichernAsync(Artikel(k.Id, "Kopierpapier A4"));
        await fabrik.SpeichernAsync(Artikel(k.Id, "Nie gekauft"));
        await BelegMitPositionAsync(k.Id, papier.Id, new DateOnly(2026, 1, 5), 4.00m);
        await BelegMitPositionAsync(k.Id, papier.Id, new DateOnly(2026, 3, 5), 5.00m);
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        var detail = (await client.GetJsonAsync<KorrespondentDetailDto>(Einzeln(k.Id))).ShouldNotBeNull();

        detail.AnzahlArtikel.ShouldBe(2);
        var artikel = detail.Artikel.ShouldHaveSingleItem();
        artikel.Kaeufe.ShouldBe(2);
        artikel.LetzterEinzelpreis.ShouldBe(5.00m);
        artikel.AenderungSeitErstkauf.ShouldBe(0.25m);
        artikel.ZuletztGekauft.ShouldBe(new DateOnly(2026, 3, 5));
    }

    [Fact]
    public async Task Anlegen_normalisiert_und_leitet_Domain_ab()
    {
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie("Büro"));
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostJsonAsync(ApiRouten.Korrespondenten.Liste, Eingabe("Neuer Lieferant") with
        {
            UstIdNr = "de 123 456 789",
            Ibans = ["de89 3704 0044 0532 0130 00", GueltigeIban],
            Aliase = ["NL", "nl", "Neuer Lieferant"],
            Website = "https://www.neuer-lieferant.de/kontakt",
            Anschrift = "Hauptstraße 1\n12345 Berlin",
            DefaultKategorieId = kategorie.Id,
        });

        antwort.StatusCode.ShouldBe(HttpStatusCode.Created);
        var detail = (await antwort.Content.LesenAsync<KorrespondentDetailDto>()).ShouldNotBeNull();
        detail.UstIdNr.ShouldBe("DE123456789");
        detail.Ibans.ShouldBe([GueltigeIban]);
        detail.Aliase.ShouldBe(["NL"]);
        detail.Anschrift.ShouldBe("Hauptstraße 1\n12345 Berlin");
        detail.DefaultKategorie.ShouldNotBeNull().Name.ShouldBe("Büro");
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var gespeichert = await db.Korrespondenten.SingleAsync(x => x.Id == detail.Id);
        gespeichert.Domain.ShouldBe("neuer-lieferant.de");
        gespeichert.Geprueft.ShouldBeTrue();
    }

    [Fact]
    public async Task Anlegen_mit_ungueltiger_Iban_oder_ohne_Name_ist_400()
    {
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var iban = await client.PostJsonAsync(ApiRouten.Korrespondenten.Liste, Eingabe("X") with { Ibans = ["DE89370400440532013001"] });
        var name = await client.PostJsonAsync(ApiRouten.Korrespondenten.Liste, Eingabe(" "));
        var kategorie = await client.PostJsonAsync(ApiRouten.Korrespondenten.Liste, Eingabe("Y") with { DefaultKategorieId = Guid.NewGuid() });

        iban.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await iban.Content.ReadAsStringAsync()).ShouldContain("ibans");
        name.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        kategorie.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Doppelte_UstIdNr_ist_409_mit_Hinweis()
    {
        await fabrik.SpeichernAsync(Testdaten.Korrespondent("Erster", "DE123456789"));
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostJsonAsync(ApiRouten.Korrespondenten.Liste, Eingabe("Zweiter") with { UstIdNr = "DE 123456789" });

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await antwort.Content.ReadAsStringAsync()).ShouldContain("Erster");
    }

    [Fact]
    public async Task Doppelte_UstIdNr_nennt_den_vorhandenen_Korrespondenten_bei_Anlegen_und_Aendern()
    {
        var erster = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Vorhanden", "DE987654321"));
        var zweiter = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Anderer"));
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var anlegen = await client.PostJsonAsync(ApiRouten.Korrespondenten.Liste, Eingabe("Neuer") with { UstIdNr = "DE987654321" });
        var aendern = await client.PutJsonAsync(Einzeln(zweiter.Id), Eingabe("Anderer") with { UstIdNr = "DE987654321" });

        foreach (var antwort in new[] { anlegen, aendern })
        {
            antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            var problem = await antwort.Content.LesenAsync<JsonElement>();
            problem.GetProperty("korrespondentId").GetGuid().ShouldBe(erster.Id);
        }
    }

    [Fact]
    public async Task Bearbeiten_setzt_geprueft_und_unbekannt_ist_404()
    {
        var k = Testdaten.Korrespondent("Ungeprüft");
        k.Geprueft = false;
        await fabrik.SpeichernAsync(k);
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PutJsonAsync(Einzeln(k.Id), Eingabe("Geprüft"));
        var unbekannt = await client.PutJsonAsync(Einzeln(Guid.NewGuid()), Eingabe("Egal"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<KorrespondentDetailDto>()).ShouldNotBeNull().Name.ShouldBe("Geprüft");
        (await ListeAsync(client, "")).ShouldHaveSingleItem().IstNeu.ShouldBeFalse();
        unbekannt.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Zusammenfuehren_haengt_alles_um_und_loescht_Quelle()
    {
        var behalten = Testdaten.Korrespondent("Bürowelt Krämer KG");
        behalten.Ibans = [GueltigeIban];
        await fabrik.SpeichernAsync(behalten);
        var quelle = Testdaten.Korrespondent("Buerowelt Kraemer", "DE999999999");
        quelle.Aliase = ["BWK"];
        quelle.Ibans = [ZweiteIban, GueltigeIban];
        quelle.Email = "info@bwk.de";
        await fabrik.SpeichernAsync(quelle);

        var papierBehalten = await fabrik.SpeichernAsync(Artikel(behalten.Id, "Kopierpapier A4"));
        var papierQuelle = await fabrik.SpeichernAsync(Artikel(quelle.Id, "Kopierpapier A4"));
        var toner = await fabrik.SpeichernAsync(Artikel(quelle.Id, "Toner"));
        var belegPapier = await BelegMitPositionAsync(quelle.Id, papierQuelle.Id, new DateOnly(2026, 2, 1), 4m);
        var belegToner = await BelegMitPositionAsync(quelle.Id, toner.Id, new DateOnly(2026, 2, 2), 30m);
        const string pfad = "2026/02/buerowelt-kraemer/RE-1.pdf";
        await fabrik.SpeichernAsync(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = belegPapier.Id,
            Rolle = DateiRolle.Original,
            Pfad = pfad,
            Sha256 = new string('a', 64),
            Groesse = 1,
            MimeType = "application/pdf",
            AbgelegtAm = fabrik.Zeit.GetUtcNow(),
        });
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostJsonAsync(
            ApiRouten.Korrespondenten.Zusammenfuehren, new KorrespondentenZusammenfuehrenDto(behalten.Id, [quelle.Id]));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await antwort.Content.LesenAsync<KorrespondentDetailDto>()).ShouldNotBeNull();
        detail.Aliase.ShouldBe(["Buerowelt Kraemer", "BWK"]);
        detail.Ibans.ShouldBe([GueltigeIban, ZweiteIban]);
        detail.UstIdNr.ShouldBe("DE999999999");
        detail.Email.ShouldBe("info@bwk.de");
        detail.Kennzahlen.AnzahlBelege.ShouldBe(2);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Korrespondenten.AnyAsync(x => x.Id == quelle.Id)).ShouldBeFalse();
        (await db.Belege.Where(b => b.Id == belegPapier.Id || b.Id == belegToner.Id).Select(b => b.KorrespondentId).ToListAsync())
            .ShouldAllBe(id => id == behalten.Id);
        (await db.Artikel.AnyAsync(a => a.Id == papierQuelle.Id)).ShouldBeFalse();
        (await db.Artikel.SingleAsync(a => a.Id == toner.Id)).KorrespondentId.ShouldBe(behalten.Id);
        (await db.Positionen.SingleAsync(p => p.BelegId == belegPapier.Id)).ArtikelId.ShouldBe(papierBehalten.Id);
        (await db.BelegDateien.SingleAsync(d => d.BelegId == belegPapier.Id)).Pfad.ShouldBe(pfad);
    }

    [Fact]
    public async Task Zusammenfuehren_mit_sich_selbst_oder_unbekannt_ist_400_und_aendert_nichts()
    {
        var behalten = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Behalten"));
        var quelle = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Quelle"));
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(korrespondentId: quelle.Id));
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        var selbst = await client.PostJsonAsync(ApiRouten.Korrespondenten.Zusammenfuehren, new KorrespondentenZusammenfuehrenDto(behalten.Id, [behalten.Id]));
        var unbekannt = await client.PostJsonAsync(ApiRouten.Korrespondenten.Zusammenfuehren, new KorrespondentenZusammenfuehrenDto(behalten.Id, [quelle.Id, Guid.NewGuid()]));
        var unbekanntBehalten = await client.PostJsonAsync(ApiRouten.Korrespondenten.Zusammenfuehren, new KorrespondentenZusammenfuehrenDto(Guid.NewGuid(), [quelle.Id]));

        selbst.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unbekannt.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unbekanntBehalten.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Belege.SingleAsync(b => b.Id == beleg.Id)).KorrespondentId.ShouldBe(quelle.Id);
        (await db.Korrespondenten.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task NurLesen_darf_nichts_aendern()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await client.PostJsonAsync(ApiRouten.Korrespondenten.Liste, Eingabe("X"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PutJsonAsync(Einzeln(k.Id), Eingabe("X"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostJsonAsync(ApiRouten.Korrespondenten.Zusammenfuehren, new KorrespondentenZusammenfuehrenDto(k.Id, [Guid.NewGuid()])))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static string Einzeln(Guid id) => ApiRouten.Korrespondenten.Einzeln.Replace("{id}", id.ToString(), StringComparison.Ordinal);

    private static async Task<List<KorrespondentListeneintragDto>> ListeAsync(HttpClient client, string query) =>
        (await client.GetJsonAsync<List<KorrespondentListeneintragDto>>(ApiRouten.Korrespondenten.Liste + query)).ShouldNotBeNull();

    private static KorrespondentAendernDto Eingabe(string name) => new(
        name, [], null, null, null, [], null, null, true, false, null, new MatchingDto(MatchingAlgorithmus.Keiner, null, false));

    private static Artikel Artikel(Guid korrespondentId, string name) => new()
    {
        Id = Guid.NewGuid(),
        KorrespondentId = korrespondentId,
        Name = name,
        NameNormalisiert = name.ToLowerInvariant(),
    };

    private async Task<Beleg> BelegMitPositionAsync(Guid korrespondentId, Guid artikelId, DateOnly datum, decimal einzelpreis)
    {
        var beleg = Testdaten.Beleg(rechnungsdatum: datum, brutto: einzelpreis, korrespondentId: korrespondentId);
        beleg.Positionen =
        [
            new Position { Id = Guid.NewGuid(), Nr = 1, Text = "Pos", Menge = 1, Einzelpreis = einzelpreis, Gesamt = einzelpreis, ArtikelId = artikelId },
        ];
        return await fabrik.SpeichernAsync(beleg);
    }
}
