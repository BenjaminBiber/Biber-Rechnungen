using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.IntegrationTests.Infrastruktur;

namespace Biber.Invoice.IntegrationTests.Suche;

[Collection("Api")]
public sealed class SucheTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Toner_findet_Beleg_Artikel_und_Stammartikel()
    {
        var lieferant = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var beleg = Testdaten.Beleg(korrespondentId: lieferant.Id);
        beleg.Positionen.Add(new Position { Id = Guid.NewGuid(), Nr = 1, Text = "Lasertoner schwarz" });
        await fabrik.SpeichernAsync(beleg);
        var stamm = await fabrik.SpeichernAsync(new Stammartikel { Id = Guid.NewGuid(), Name = "Toner schwarz", Basiseinheit = "Stk" });
        var artikel = await fabrik.SpeichernAsync(new Artikel
        {
            Id = Guid.NewGuid(),
            KorrespondentId = lieferant.Id,
            Name = "Lasertoner schwarz",
            NameNormalisiert = "lasertoner schwarz",
            StammartikelId = stamm.Id,
        });

        var ergebnis = await SucheAsync("toner");

        var gefundenerBeleg = ergebnis.Belege.Single(b => b.Id == beleg.Id);
        gefundenerBeleg.Auszug.ShouldBe("Lasertoner schwarz");
        ergebnis.Artikel.ShouldContain(a => a.Id == artikel.Id);
        var gefundenerStamm = ergebnis.Stammartikel.Single(s => s.Id == stamm.Id);
        gefundenerStamm.AnzahlArtikel.ShouldBe(1);
        gefundenerStamm.Korrespondenten.ShouldBe([lieferant.Name]);
    }

    [Fact]
    public async Task Kraemer_Praefix_findet_Korrespondent_mit_Umlaut()
    {
        var kraemer = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Bürowelt Krämer KG"));

        var ergebnis = await SucheAsync("krä");

        ergebnis.Korrespondenten.ShouldContain(k => k.Id == kraemer.Id);
    }

    [Fact]
    public async Task Korrespondent_wird_ueber_Alias_und_UstIdNr_gefunden()
    {
        var korrespondent = Testdaten.Korrespondent("Quuxwerk GmbH", ustIdNr: "DE987654321");
        korrespondent.Aliase = ["Zorbix Handel"];
        await fabrik.SpeichernAsync(korrespondent);

        (await SucheAsync("zorbix")).Korrespondenten.ShouldContain(k => k.Id == korrespondent.Id);
        (await SucheAsync("987654321")).Korrespondenten.ShouldContain(k => k.Id == korrespondent.Id);
    }

    [Fact]
    public async Task Rechnungsnummer_Teil_findet_Beleg()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsnummer: "RE-2026-48817"));

        var ergebnis = await SucheAsync("48817");

        ergebnis.Belege.ShouldContain(b => b.Id == beleg.Id);
    }

    [Fact]
    public async Task Volltext_Treffer_liefert_Textstelle_als_Auszug()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Wartungsvertrag Kaffeemaschine Quartal 3, Pauschale"));

        var ergebnis = await SucheAsync("kaffeemaschine");

        ergebnis.Belege.Single(b => b.Id == beleg.Id).Auszug.ShouldContain("Kaffeemaschine");
    }

    [Theory]
    [InlineData("")]
    [InlineData("t")]
    [InlineData("  t  ")]
    public async Task Unter_zwei_Zeichen_ist_das_Ergebnis_leer(string q)
    {
        await fabrik.SpeichernAsync(Testdaten.Korrespondent("Tau GmbH"));

        var ergebnis = await SucheAsync(q);

        ergebnis.Belege.ShouldBeEmpty();
        ergebnis.Korrespondenten.ShouldBeEmpty();
        ergebnis.Artikel.ShouldBeEmpty();
        ergebnis.Stammartikel.ShouldBeEmpty();
    }

    [Fact]
    public async Task Je_Gruppe_hoechstens_fuenf_Treffer()
    {
        var marke = "Maxmarke" + Guid.NewGuid().ToString("N")[..8].Replace('0', 'x');
        var lieferant = await fabrik.SpeichernAsync(Testdaten.Korrespondent($"{marke} Lieferant"));
        for (var i = 0; i < 7; i++)
        {
            await fabrik.SpeichernAsync(Testdaten.Korrespondent($"{marke} Firma {i}"));
            await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsnummer: $"{marke}-{i}"));
            await fabrik.SpeichernAsync(new Stammartikel { Id = Guid.NewGuid(), Name = $"{marke} Stamm {i}", Basiseinheit = "Stk" });
            await fabrik.SpeichernAsync(new Artikel
            {
                Id = Guid.NewGuid(),
                KorrespondentId = lieferant.Id,
                Name = $"{marke} Artikel {i}",
                NameNormalisiert = $"{marke} artikel {i}".ToLowerInvariant(),
            });
        }

        var ergebnis = await SucheAsync(marke);

        ergebnis.Belege.Length.ShouldBe(5);
        ergebnis.Korrespondenten.Length.ShouldBe(5);
        ergebnis.Artikel.Length.ShouldBe(5);
        ergebnis.Stammartikel.Length.ShouldBe(5);
    }

    [Fact]
    public async Task Beleg_mit_mehreren_Treffern_erscheint_nur_einmal()
    {
        var marke = "Dublettenmarke" + Guid.NewGuid().ToString("N")[..8].Replace('0', 'x');
        var beleg = Testdaten.Beleg(rechnungsnummer: $"{marke}-1", volltext: $"{marke} {marke}");
        beleg.Positionen.Add(new Position { Id = Guid.NewGuid(), Nr = 1, Text = $"{marke} eins" });
        beleg.Positionen.Add(new Position { Id = Guid.NewGuid(), Nr = 2, Text = $"{marke} zwei" });
        await fabrik.SpeichernAsync(beleg);

        var ergebnis = await SucheAsync(marke);

        ergebnis.Belege.Count(b => b.Id == beleg.Id).ShouldBe(1);
        ergebnis.Belege.ShouldHaveSingleItem().Auszug.ShouldBe($"{marke} eins");
    }

    [Theory]
    [InlineData("o'brien")]
    [InlineData("a:b")]
    [InlineData("müller & söhne")]
    [InlineData("100% _ \\")]
    [InlineData("!!(|)<>-")]
    [InlineData("\"offen")]
    public async Task Sonderzeichen_fuehren_nicht_zu_einem_Fehler(string q)
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer).GetAsync($"{ApiRouten.Suche.Global}?q={Uri.EscapeDataString(q)}");

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Zu_langer_Suchbegriff_liefert_400()
    {
        var antwort = await fabrik.ErzeugeClient().GetAsync($"{ApiRouten.Suche.Global}?q={new string('a', 101)}");

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NurLesen_darf_suchen()
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync($"{ApiRouten.Suche.Global}?q=toner");

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Anonym_liefert_401()
    {
        var antwort = await fabrik.ErzeugeAnonymenClient().GetAsync($"{ApiRouten.Suche.Global}?q=toner");

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<SuchergebnisDto> SucheAsync(string q)
    {
        var antwort = await fabrik.ErzeugeClient().GetAsync($"{ApiRouten.Suche.Global}?q={Uri.EscapeDataString(q)}");
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK, await antwort.Content.ReadAsStringAsync());
        return (await antwort.Content.ReadFromJsonAsync<SuchergebnisDto>())!;
    }
}
