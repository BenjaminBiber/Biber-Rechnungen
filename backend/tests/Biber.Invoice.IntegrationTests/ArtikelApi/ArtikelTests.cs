using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Artikel;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ArtikelEntitaet = Biber.Invoice.Domain.Entitaeten.Artikel;

namespace Biber.Invoice.IntegrationTests.ArtikelApi;

/// <summary>
/// Daten wie im Design: Kopierpapier bei zwei Lieferanten mit sechs Käufen
/// (Karton à 2.500 Blatt: 22,99 → 24,90 €; Pack à 500 Blatt: 5,29 → 5,49 €).
/// </summary>
[Collection("Api")]
public sealed class ArtikelTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Liste_zeigt_Kaeufe_letzten_Preis_und_Aenderung_seit_Erstkauf()
    {
        var s = await SzenarioAsync(mitStammartikel: false);

        var seite = await ListeAsync($"?korrespondentId={s.Buero.Id}");

        var karton = seite.Eintraege.ShouldHaveSingleItem();
        karton.Id.ShouldBe(s.Karton.Id);
        karton.Kaeufe.ShouldBe(4);
        karton.LetzterEinzelpreis.ShouldBe(24.90m);
        karton.Einheit.ShouldBe("Karton");
        karton.AenderungSeitErstkauf.ShouldBe(0.0831m);
        karton.ZuletztGekauft.ShouldBe(new DateOnly(2025, 9, 1));
        karton.Stammartikel.ShouldBeNull();
        karton.Korrespondent.ShouldBe(new VerweisDto(s.Buero.Id, s.Buero.Name));
    }

    [Fact]
    public async Task Dubletten_und_nicht_fertige_Belege_zaehlen_nicht()
    {
        var s = await SzenarioAsync(mitStammartikel: false);
        var original = await fabrik.SpeichernAsync(Testdaten.Beleg(korrespondentId: s.Buero.Id));
        var dublette = Beleg(s.Buero, new DateOnly(2025, 12, 1), (s.Karton, 1m, 99.00m));
        dublette.DuplikatVonId = original.Id;
        await fabrik.SpeichernAsync(dublette);
        await fabrik.SpeichernAsync(Beleg(s.Buero, new DateOnly(2025, 12, 2), BelegStatus.InVerarbeitung, (s.Karton, 1m, 98.00m)));

        var karton = (await ListeAsync($"?korrespondentId={s.Buero.Id}")).Eintraege.Single();

        karton.Kaeufe.ShouldBe(4);
        karton.LetzterEinzelpreis.ShouldBe(24.90m);
        karton.ZuletztGekauft.ShouldBe(new DateOnly(2025, 9, 1));
    }

    [Fact]
    public async Task Filter_Suche_ohne_Stammartikel_und_Preisaenderung()
    {
        var s = await SzenarioAsync(mitStammartikel: true);
        var lose = await fabrik.SpeichernAsync(NeuerArtikel(s.Papier, $"Heftklammern {s.Marke}", "Pack"));
        await fabrik.SpeichernAsync(Beleg(s.Papier, new DateOnly(2025, 4, 1), (lose, 1m, 3.00m)));
        await fabrik.SpeichernAsync(Beleg(s.Papier, new DateOnly(2025, 6, 1), (lose, 1m, 2.50m)));

        (await ListeAsync($"?suche={s.Marke}")).Eintraege.Select(a => a.Id)
            .ShouldBe([s.Karton.Id, s.Pack.Id, lose.Id], ignoreOrder: true);
        (await ListeAsync($"?suche=heftklammern%20{s.Marke}")).Eintraege.ShouldHaveSingleItem().Id.ShouldBe(lose.Id);
        (await ListeAsync($"?korrespondentId={s.Papier.Id}&ohneStammartikel=true")).Eintraege.ShouldHaveSingleItem().Id.ShouldBe(lose.Id);
        (await ListeAsync($"?korrespondentId={s.Papier.Id}&preisaenderung=Gefallen")).Eintraege.ShouldHaveSingleItem().Id.ShouldBe(lose.Id);
        (await ListeAsync($"?korrespondentId={s.Papier.Id}&preisaenderung=Gestiegen")).Eintraege.ShouldHaveSingleItem().Id.ShouldBe(s.Pack.Id);
    }

    [Fact]
    public async Task Liste_ist_blaetterbar_und_nach_letztem_Kauf_sortiert()
    {
        var s = await SzenarioAsync(mitStammartikel: false);
        var neu = await fabrik.SpeichernAsync(NeuerArtikel(s.Buero, $"Ordner {s.Marke}", "Stk"));
        await fabrik.SpeichernAsync(Beleg(s.Buero, new DateOnly(2025, 10, 1), (neu, 5m, 1.99m)));

        var erste = await ListeAsync($"?korrespondentId={s.Buero.Id}&seitengroesse=1");
        var zweite = await ListeAsync($"?korrespondentId={s.Buero.Id}&seitengroesse=1&seite=2");

        erste.Gesamt.ShouldBe(2);
        erste.Eintraege.Single().Id.ShouldBe(neu.Id);
        zweite.Eintraege.Single().Id.ShouldBe(s.Karton.Id);
        (await Client().GetAsync($"{ApiRouten.Artikel.Liste}?seite=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Artikel_Detail_hat_genau_eine_Serie()
    {
        var s = await SzenarioAsync(mitStammartikel: true);

        var detail = await DetailAsync(ApiRouten.Artikel.Einzeln, s.Karton.Id, 2025);

        detail.IstStammartikel.ShouldBeFalse();
        var serie = detail.Serien.ShouldHaveSingleItem();
        serie.Korrespondent.Id.ShouldBe(s.Buero.Id);
        serie.Punkte.Select(p => p.Einzelpreis).ShouldBe([22.99m, 23.49m, 23.99m, 24.90m]);
        serie.Punkte.Last().Grundpreis.ShouldBe(1.00m);
        detail.Kennzahlen.AnzahlKaeufe.ShouldBe(4);
        detail.Kennzahlen.MengeJahr.ShouldBe(9m);
        detail.Kennzahlen.AenderungSeitJahresbeginn.ShouldBe(0.0831m);
        detail.Kaeufe.First().Datum.ShouldBe(new DateOnly(2025, 9, 1));
        (await Client().GetAsync(ApiRouten.Artikel.Einzeln.Replace("{id}", Guid.NewGuid().ToString())))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stammartikel_Liste_nennt_guenstigsten_Preis_beim_richtigen_Lieferanten()
    {
        var s = await SzenarioAsync(mitStammartikel: true);

        var liste = await Client().GetJsonAsync<StammartikelListeneintragDto[]>(ApiRouten.Stammartikel.Liste);

        var eintrag = liste!.Single(e => e.Id == s.Stamm!.Id);
        eintrag.AnzahlArtikel.ShouldBe(2);
        eintrag.Korrespondenten.ShouldBe([s.Buero.Name, s.Papier.Name], ignoreOrder: true);
        eintrag.GuenstigsterPreis.ShouldBe(1.00m);
        eintrag.GuenstigsterBei.ShouldBe(s.Buero.Name);
        eintrag.Verlauf.Length.ShouldBe(6);
        eintrag.Verlauf.Last().ShouldBe(1.00m);
        eintrag.Verlauf.First().ShouldBe(0.92m);
    }

    [Fact]
    public async Task Stammartikel_Detail_mit_zwei_Serien_Kennzahlen_und_Kaeufen()
    {
        var s = await SzenarioAsync(mitStammartikel: true);

        var detail = await DetailAsync(ApiRouten.Stammartikel.Einzeln, s.Stamm!.Id, 2025);

        detail.IstStammartikel.ShouldBeTrue();
        detail.Basiseinheit.ShouldBe("Blatt");
        detail.GrundpreisMenge.ShouldBe(100m);
        detail.Serien.Length.ShouldBe(2);
        detail.Serien.Single(x => x.Korrespondent.Id == s.Pack.KorrespondentId).Punkte.Last().Grundpreis.ShouldBe(1.10m);
        detail.Kennzahlen.GuenstigsterGrundpreis.ShouldBe(1.00m);
        detail.Kennzahlen.GuenstigsterBei.ShouldBe(s.Buero.Name);
        detail.Kennzahlen.MengeJahr.ShouldBe(23500m);
        detail.Kennzahlen.AnzahlKaeufe.ShouldBe(6);
        detail.Kennzahlen.AusgabenJahrNetto.ShouldBe((2 * 22.99m) + (2 * 23.49m) + (2 * 23.99m) + (3 * 24.90m) + 5.29m + 5.49m);
        detail.Kennzahlen.PreisVon.ShouldBe(0.92m);
        detail.Kennzahlen.PreisBis.ShouldBe(1.00m);
        detail.Kaeufe.Length.ShouldBe(6);
        detail.Kaeufe.Select(k => k.Datum).ShouldBeInOrder(SortDirection.Descending);
        detail.Artikel.Select(a => a.UmrechnungFaktor).ShouldBe([2500m, 500m], ignoreOrder: true);

        var anderesJahr = await DetailAsync(ApiRouten.Stammartikel.Einzeln, s.Stamm.Id, 2024);
        anderesJahr.Kennzahlen.AnzahlKaeufe.ShouldBe(0);
        anderesJahr.Kaeufe.ShouldBeEmpty();
    }

    [Fact]
    public async Task Anlegen_Faktor_aendern_herausloesen_und_letzter_Artikel_loescht()
    {
        var s = await SzenarioAsync(mitStammartikel: false);
        var client = Client();

        var anlegen = await client.PostJsonAsync(ApiRouten.Stammartikel.Liste, new StammartikelAendernDto(
            $"Kopierpapier {s.Marke}", "Blatt", 100m, [new ArtikelZuordnungDto(s.Karton.Id, 2500m)]));
        anlegen.StatusCode.ShouldBe(HttpStatusCode.Created);
        var stamm = (await anlegen.Content.LesenAsync<StammartikelListeneintragDto>())!;
        stamm.AnzahlArtikel.ShouldBe(1);
        stamm.GuenstigsterPreis.ShouldBe(1.00m);
        var route = ApiRouten.Stammartikel.Einzeln.Replace("{id}", stamm.Id.ToString());

        var aendern = await client.PutJsonAsync(route, new StammartikelAendernDto(
            $"Kopierpapier {s.Marke}", "Blatt", 100m,
            [new ArtikelZuordnungDto(s.Karton.Id, 5000m), new ArtikelZuordnungDto(s.Pack.Id, 500m)]));
        aendern.StatusCode.ShouldBe(HttpStatusCode.OK);
        var geaendert = (await aendern.Content.LesenAsync<StammartikelListeneintragDto>())!;
        geaendert.AnzahlArtikel.ShouldBe(2);
        geaendert.GuenstigsterPreis.ShouldBe(0.50m);

        (await client.DeleteAsync(ArtikelRoute(stamm.Id, s.Karton.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LadeArtikelAsync(s.Karton.Id)).ShouldSatisfyAllConditions(
            a => a.StammartikelId.ShouldBeNull(),
            a => a.UmrechnungFaktor.ShouldBeNull());
        (await client.GetAsync(route)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.DeleteAsync(ArtikelRoute(stamm.Id, s.Pack.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync(route)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync(ArtikelRoute(stamm.Id, s.Pack.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Artikel_doppelt_zuordnen_ergibt_409()
    {
        var s = await SzenarioAsync(mitStammartikel: true);

        var antwort = await Client().PostJsonAsync(ApiRouten.Stammartikel.Liste, new StammartikelAendernDto(
            $"Anderes Papier {s.Marke}", "Blatt", 1m, [new ArtikelZuordnungDto(s.Karton.Id, 2500m)]));

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LadeArtikelAsync(s.Karton.Id)).StammartikelId.ShouldBe(s.Stamm!.Id);
    }

    [Fact]
    public async Task Derselbe_Stammartikel_darf_seinen_Artikel_erneut_nennen()
    {
        var s = await SzenarioAsync(mitStammartikel: true);

        var antwort = await Client().PutJsonAsync(
            ApiRouten.Stammartikel.Einzeln.Replace("{id}", s.Stamm!.Id.ToString()),
            new StammartikelAendernDto($"Kopierpapier A4 {s.Marke}", "Blatt", 100m, [new ArtikelZuordnungDto(s.Karton.Id, 2500m)]));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<StammartikelListeneintragDto>())!.AnzahlArtikel.ShouldBe(2);
    }

    [Theory]
    [InlineData("", "Blatt", 100, 2500, true)]
    [InlineData("Papier", " ", 100, 2500, true)]
    [InlineData("Papier", "Blatt", 0, 2500, true)]
    [InlineData("Papier", "Blatt", 100, 0, true)]
    [InlineData("Papier", "Blatt", 100, 2500, false)]
    public async Task Ungueltige_Eingaben_ergeben_400(string name, string basiseinheit, decimal menge, decimal faktor, bool mitArtikel)
    {
        var s = await SzenarioAsync(mitStammartikel: false);
        ArtikelZuordnungDto[] artikel = mitArtikel ? [new ArtikelZuordnungDto(s.Karton.Id, faktor)] : [];

        var antwort = await Client().PostJsonAsync(
            ApiRouten.Stammartikel.Liste, new StammartikelAendernDto(name, basiseinheit, menge, artikel));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unbekannter_Artikel_ergibt_400_und_unbekannter_Stammartikel_404()
    {
        var client = Client();
        var dto = new StammartikelAendernDto("Papier", "Blatt", 100m, [new ArtikelZuordnungDto(Guid.NewGuid(), 1m)]);

        (await client.PostJsonAsync(ApiRouten.Stammartikel.Liste, dto)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PutJsonAsync(ApiRouten.Stammartikel.Einzeln.Replace("{id}", Guid.NewGuid().ToString()), dto))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NurLesen_darf_lesen_aber_nicht_aendern()
    {
        var s = await SzenarioAsync(mitStammartikel: true);
        var client = Client(Rolle.NurLesen);
        var dto = new StammartikelAendernDto("Papier", "Blatt", 100m, [new ArtikelZuordnungDto(s.Karton.Id, 2500m)]);
        var route = ApiRouten.Stammartikel.Einzeln.Replace("{id}", s.Stamm!.Id.ToString());

        (await client.GetAsync(ApiRouten.Artikel.Liste)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync(route)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostJsonAsync(ApiRouten.Stammartikel.Liste, dto)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PutJsonAsync(route, dto)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync(ArtikelRoute(s.Stamm.Id, s.Karton.Id))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Kaeufe_nennen_die_Merkmale_der_Position()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var schuh = await fabrik.SpeichernAsync(NeuerArtikel(korrespondent, "S3 Sicherheitshalbschuhe", "Paar"));
        var beleg = Beleg(korrespondent, new DateOnly(2026, 11, 25), (schuh, 1m, 56.90m), (schuh, 1m, 56.90m));
        beleg.Positionen[0].Merkmale = [new Merkmal("Größe", "47"), new Merkmal("Farbe", "grau")];
        beleg.Positionen[1].Merkmale = [new Merkmal("Größe", "42"), new Merkmal("Farbe", "grau")];
        await fabrik.SpeichernAsync(beleg);

        var detail = await DetailAsync(ApiRouten.Artikel.Einzeln, schuh.Id, 2026);

        detail.Kaeufe.Select(k => string.Join(", ", k.Merkmale.Select(m => $"{m.Name}: {m.Wert}")))
            .Order().ShouldBe(["Größe: 42, Farbe: grau", "Größe: 47, Farbe: grau"]);
    }

    private HttpClient Client(Rolle rolle = Rolle.Benutzer) => fabrik.ErzeugeClient(rolle);

    private static string ArtikelRoute(Guid stammId, Guid artikelId) =>
        ApiRouten.Stammartikel.Artikel.Replace("{id}", stammId.ToString()).Replace("{artikelId}", artikelId.ToString());

    private async Task<SeiteDto<ArtikelListeneintragDto>> ListeAsync(string query) =>
        (await Client().GetJsonAsync<SeiteDto<ArtikelListeneintragDto>>(ApiRouten.Artikel.Liste + query))!;

    private async Task<PreisDetailDto> DetailAsync(string route, Guid id, int jahr) =>
        (await Client().GetJsonAsync<PreisDetailDto>($"{route.Replace("{id}", id.ToString())}?jahr={jahr}"))!;

    private async Task<ArtikelEntitaet> LadeArtikelAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Artikel.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    private async Task<Szenario> SzenarioAsync(bool mitStammartikel)
    {
        var marke = "Kp" + Guid.NewGuid().ToString("N")[..8].Replace('0', 'x');
        var buero = await fabrik.SpeichernAsync(Testdaten.Korrespondent($"Bürobedarf {marke}"));
        var papier = await fabrik.SpeichernAsync(Testdaten.Korrespondent($"Papierhaus {marke}"));
        Stammartikel? stamm = null;
        if (mitStammartikel)
        {
            stamm = await fabrik.SpeichernAsync(new Stammartikel
            {
                Id = Guid.NewGuid(),
                Name = $"Kopierpapier A4 {marke}",
                Basiseinheit = "Blatt",
                GrundpreisMenge = 100m,
            });
        }

        var karton = NeuerArtikel(buero, $"Kopierpapier A4 80g Karton {marke}", "Karton");
        var pack = NeuerArtikel(papier, $"Kopierpapier 500 Blatt {marke}", "Pack");
        if (stamm is not null)
        {
            (karton.StammartikelId, karton.UmrechnungFaktor) = (stamm.Id, 2500m);
            (pack.StammartikelId, pack.UmrechnungFaktor) = (stamm.Id, 500m);
        }

        await fabrik.SpeichernAsync(karton);
        await fabrik.SpeichernAsync(pack);

        await fabrik.SpeichernAsync(Beleg(buero, new DateOnly(2025, 2, 10), (karton, 2m, 22.99m)));
        await fabrik.SpeichernAsync(Beleg(papier, new DateOnly(2025, 3, 5), (pack, 1m, 5.29m)));
        await fabrik.SpeichernAsync(Beleg(buero, new DateOnly(2025, 4, 14), (karton, 2m, 23.49m)));
        await fabrik.SpeichernAsync(Beleg(buero, new DateOnly(2025, 6, 23), (karton, 2m, 23.99m)));
        await fabrik.SpeichernAsync(Beleg(papier, new DateOnly(2025, 8, 20), (pack, 1m, 5.49m)));
        await fabrik.SpeichernAsync(Beleg(buero, new DateOnly(2025, 9, 1), (karton, 3m, 24.90m)));

        return new Szenario(marke, buero, papier, stamm, karton, pack);
    }

    private static ArtikelEntitaet NeuerArtikel(Korrespondent korrespondent, string name, string einheit) => new()
    {
        Id = Guid.NewGuid(),
        KorrespondentId = korrespondent.Id,
        Name = name,
        NameNormalisiert = name.ToLowerInvariant(),
        Einheit = einheit,
    };

    private static Beleg Beleg(Korrespondent korrespondent, DateOnly datum, params (ArtikelEntitaet Artikel, decimal Menge, decimal Preis)[] positionen) =>
        Beleg(korrespondent, datum, BelegStatus.Fertig, positionen);

    private static Beleg Beleg(
        Korrespondent korrespondent,
        DateOnly datum,
        BelegStatus status,
        params (ArtikelEntitaet Artikel, decimal Menge, decimal Preis)[] positionen)
    {
        var beleg = Testdaten.Beleg(rechnungsdatum: datum, korrespondentId: korrespondent.Id, status: status);
        var nr = 1;
        foreach (var (artikel, menge, preis) in positionen)
        {
            beleg.Positionen.Add(new Position
            {
                Id = Guid.NewGuid(),
                Nr = nr++,
                Text = artikel.Name,
                Menge = menge,
                Einheit = artikel.Einheit,
                Einzelpreis = preis,
                Gesamt = menge * preis,
                ArtikelId = artikel.Id,
            });
        }

        return beleg;
    }

    private sealed record Szenario(
        string Marke,
        Korrespondent Buero,
        Korrespondent Papier,
        Stammartikel? Stamm,
        ArtikelEntitaet Karton,
        ArtikelEntitaet Pack);
}
