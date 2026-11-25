using System.Net;
using System.Security.Cryptography;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Belege.Schreiben;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.Belege;

[Collection("Api")]
public sealed class BelegSchreibTests(ApiFabrik fabrik) : IAsyncLifetime
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

        var bezahlt = Testdaten.Tag("bezahlt", "gruen");
        bezahlt.IstZahlungsTag = true;
        _zahlungsTagId = (await fabrik.SpeichernAsync(bezahlt)).Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- Korrektur von Kopf und Steuerzeilen ----------

    [Fact]
    public async Task Korrektur_von_Kopf_und_Steuerzeilen_wird_gespeichert()
    {
        var beleg = await BelegAsync();
        var aenderung = Aenderung(beleg) with
        {
            Rechnungsnummer = " RE-4711 ",
            Titel = "Druckerpapier",
            Rechnungsdatum = new DateOnly(2026, 1, 12),
            LeistungVon = new DateOnly(2026, 1, 1),
            LeistungBis = new DateOnly(2026, 1, 31),
            Faelligkeitsdatum = new DateOnly(2026, 2, 11),
            Waehrung = "eur",
            Netto = 150m,
            Steuer = 23.50m,
            Brutto = 173.50m,
            Steuerzeilen = [new(19m, 100m, 19m), new(7m, 50m, 3.50m)],
        };

        var antwort = await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), aenderung);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await antwort.Content.LesenAsync<BelegDetailDto>()).ShouldNotBeNull();
        detail.Rechnungsnummer.ShouldBe("RE-4711");
        detail.Titel.ShouldBe("Druckerpapier");
        detail.Brutto.ShouldBe(173.50m);
        detail.Steuerzeilen.Length.ShouldBe(2);

        var gespeichert = await LadenAsync(beleg.Id);
        gespeichert.Rechnungsnummer.ShouldBe("RE-4711");
        gespeichert.Rechnungsdatum.ShouldBe(new DateOnly(2026, 1, 12));
        gespeichert.LeistungVon.ShouldBe(new DateOnly(2026, 1, 1));
        gespeichert.LeistungBis.ShouldBe(new DateOnly(2026, 1, 31));
        gespeichert.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 2, 11));
        gespeichert.Waehrung.ShouldBe("EUR");
        gespeichert.Netto.ShouldBe(150m);
        gespeichert.Steuer.ShouldBe(23.50m);
        gespeichert.Steuerzeilen.OrderBy(z => z.Satz).Select(z => (z.Satz, z.Netto, z.Steuer))
            .ShouldBe([(7m, 50m, 3.50m), (19m, 100m, 19m)]);
    }

    public static TheoryData<string, Func<BelegAendernDto, BelegAendernDto>> UngueltigeAenderungen() => new()
    {
        { "waehrung", a => a with { Waehrung = "EU" } },
        { "waehrung", a => a with { Waehrung = "E1R" } },
        { "brutto", a => a with { Brutto = -1m } },
        { "rechnungsdatum", a => a with { Rechnungsdatum = new DateOnly(1999, 12, 31) } },
        { "faelligkeitsdatum", a => a with { Faelligkeitsdatum = new DateOnly(2101, 1, 1) } },
        { "steuerzeilen[0]", a => a with { Steuerzeilen = [new(19m, -10m, -1.90m)] } },
        { "korrespondentId", a => a with { KorrespondentId = Guid.NewGuid() } },
        { "kategorieId", a => a with { KategorieId = Guid.NewGuid() } },
    };

    [Theory]
    [MemberData(nameof(UngueltigeAenderungen))]
    public async Task Ungueltige_Korrektur_ergibt_400(string feld, Func<BelegAendernDto, BelegAendernDto> aendern)
    {
        var beleg = await BelegAsync();

        var antwort = await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), aendern(Aenderung(beleg)));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await antwort.Content.ReadAsStringAsync()).ShouldContain($"\"{feld}\"");
        (await LadenAsync(beleg.Id)).Brutto.ShouldBe(beleg.Brutto);
    }

    [Fact]
    public async Task Gutschrift_darf_negative_Betraege_haben()
    {
        var beleg = await BelegAsync();
        var aenderung = Aenderung(beleg) with
        {
            Belegart = Belegart.Gutschrift,
            Netto = -100m,
            Steuer = -19m,
            Brutto = -119m,
            Steuerzeilen = [new(19m, -100m, -19m)],
        };

        var antwort = await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), aenderung);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LadenAsync(beleg.Id)).Brutto.ShouldBe(-119m);
    }

    [Fact]
    public async Task Kategorie_manuell_gesetzt_und_wieder_geleert_bringt_die_Default_Kategorie_zurueck()
    {
        var standard = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var andere = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var korrespondent = Testdaten.Korrespondent();
        korrespondent.DefaultKategorieId = standard.Id;
        await fabrik.SpeichernAsync(korrespondent);
        var beleg = await BelegAsync(b => (b.KorrespondentId, b.KategorieId) = (korrespondent.Id, standard.Id));
        var route = Route(ApiRouten.Belege.Einzeln, beleg.Id);

        (await Client().PutJsonAsync(route, Aenderung(beleg) with { KategorieId = andere.Id })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var gesetzt = await LadenAsync(beleg.Id);
        gesetzt.KategorieId.ShouldBe(andere.Id);
        gesetzt.KategorieManuell.ShouldBeTrue();

        // Unveränderte Kategorie lässt den Schutz stehen.
        (await Client().PutJsonAsync(route, Aenderung(gesetzt) with { Titel = "Anders" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LadenAsync(beleg.Id)).KategorieManuell.ShouldBeTrue();

        var antwort = await Client().PutJsonAsync(route, Aenderung(gesetzt) with { KategorieId = null });

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var geleert = await LadenAsync(beleg.Id);
        geleert.KategorieId.ShouldBe(standard.Id);
        geleert.KategorieManuell.ShouldBeFalse();
        (await antwort.Content.LesenAsync<BelegDetailDto>())!.KategorieManuell.ShouldBeFalse();
    }

    [Fact]
    public async Task Korrespondent_gewechselt_wird_manuell_geprueft_und_ordnet_die_Artikel_neu_zu()
    {
        var alt = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var neu = Testdaten.Korrespondent();
        neu.Geprueft = false;
        await fabrik.SpeichernAsync(neu);
        var beleg = await BelegAsync(b =>
        {
            b.KorrespondentId = alt.Id;
            b.KorrespondentErkennung = KorrespondentErkennung.UstIdNr;
            b.Positionen = [Position(1, "Kopierpapier A4", 100m)];
        });
        (await Client().PutJsonAsync(Route(ApiRouten.Belege.Positionen, beleg.Id), new PositionAendernDto[]
        {
            new(1, "Kopierpapier A4", 1m, null, 100m, 100m, 19m),
        })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LadenAsync(beleg.Id)).Positionen.ShouldHaveSingleItem().ArtikelId.ShouldNotBeNull();

        var antwort = await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), Aenderung(beleg) with { KorrespondentId = neu.Id });

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var gespeichert = await LadenAsync(beleg.Id);
        gespeichert.KorrespondentId.ShouldBe(neu.Id);
        gespeichert.KorrespondentErkennung.ShouldBe(KorrespondentErkennung.Manuell);
        (await KorrespondentAsync(neu.Id)).Geprueft.ShouldBeTrue();
        var position = gespeichert.Positionen.ShouldHaveSingleItem();
        position.ArtikelId.ShouldNotBeNull();
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Artikel.SingleAsync(a => a.Id == position.ArtikelId)).KorrespondentId.ShouldBe(neu.Id);
        (await db.Artikel.AnyAsync(a => a.KorrespondentId == alt.Id)).ShouldBeFalse();
    }

    // ---------- Positionen ----------

    [Fact]
    public async Task Nach_Korrektur_der_fehlenden_Position_ist_der_Befund_ok_aber_zu_pruefen_bleibt()
    {
        var beleg = await BelegAsync(b =>
        {
            b.Netto = 100m;
            b.Steuer = 19m;
            b.Brutto = 119m;
            b.Positionen = [Position(1, "Papier", 60m)];
            b.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }];
            b.Befunde = [Befund(PruefungArt.Positionssumme, PruefErgebnis.Fehlgeschlagen)];
        });

        var antwort = await Client().PutJsonAsync(
            Route(ApiRouten.Belege.Positionen, beleg.Id),
            new PositionAendernDto[]
            {
                new(7, "Papier", 2m, "Pck", 30m, 60m, 19m),
                new(9, "Toner", 1m, "Stk", 40m, 40m, 19m),
            });

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await antwort.Content.LesenAsync<BelegDetailDto>()).ShouldNotBeNull();
        detail.Positionen.Length.ShouldBe(2);
        var gespeichert = await LadenAsync(beleg.Id);
        gespeichert.Positionen.OrderBy(p => p.Nr).Select(p => (p.Nr, p.Text)).ShouldBe([(1, "Papier"), (2, "Toner")]);
        gespeichert.Befunde.Single(b => b.Pruefung == PruefungArt.Positionssumme).Ergebnis.ShouldBe(PruefErgebnis.Ok);
        gespeichert.Tags.ShouldContain(t => t.TagId == SystemIds.TagZuPruefen);
    }

    [Fact]
    public async Task Umbenannte_Position_entfernt_den_verwaisten_Artikel_samt_leerem_Stammartikel()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var beleg = await BelegAsync(b => b.KorrespondentId = korrespondent.Id);
        var anderer = await BelegAsync(b => b.KorrespondentId = korrespondent.Id);
        var client = Client();
        (await client.PutJsonAsync(Route(ApiRouten.Belege.Positionen, beleg.Id), new PositionAendernDto[]
        {
            new(1, "Papier", 1m, "Pck", 30m, 30m, 19m),
            new(2, "Toner", 1m, "Stk", 40m, 40m, 19m),
        })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PutJsonAsync(Route(ApiRouten.Belege.Positionen, anderer.Id), new PositionAendernDto[]
        {
            new(1, "Toner", 1m, "Stk", 40m, 40m, 19m),
        })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var papierId = (await LadenAsync(beleg.Id)).Positionen.Single(p => p.Text == "Papier").ArtikelId!.Value;
        var stamm = new Stammartikel { Id = Guid.NewGuid(), Name = "Papier", Basiseinheit = "Blatt", GrundpreisMenge = 100 };
        await fabrik.SpeichernAsync(stamm);
        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            await db.Artikel.Where(a => a.Id == papierId).ExecuteUpdateAsync(s => s.SetProperty(a => a.StammartikelId, stamm.Id));
        }

        var antwort = await client.PutJsonAsync(Route(ApiRouten.Belege.Positionen, beleg.Id), new PositionAendernDto[]
        {
            new(1, "Kopierpapier", 1m, "Pck", 30m, 30m, 19m),
        });

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var pruefScope = fabrik.ErzeugeScope();
        var pruefDb = pruefScope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var namen = await pruefDb.Artikel.Where(a => a.KorrespondentId == korrespondent.Id).Select(a => a.Name).ToListAsync();
        namen.Order().ShouldBe(["Kopierpapier", "Toner"]);
        (await pruefDb.Stammartikel.AnyAsync(s => s.Id == stamm.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task Position_ohne_Text_ergibt_400()
    {
        var beleg = await BelegAsync();

        var antwort = await Client().PutJsonAsync(
            Route(ApiRouten.Belege.Positionen, beleg.Id), new PositionAendernDto[] { new(1, " ", null, null, null, 10m, null) });

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------- Bestätigen ----------

    [Fact]
    public async Task Bestaetigen_entfernt_den_Tag_behaelt_die_Befunde_und_protokolliert_die_Begruendung()
    {
        var korrespondent = Testdaten.Korrespondent();
        korrespondent.Geprueft = false;
        await fabrik.SpeichernAsync(korrespondent);
        var beleg = await BelegAsync(b =>
        {
            b.KorrespondentId = korrespondent.Id;
            b.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }];
            b.Befunde = [Befund(PruefungArt.Positionssumme, PruefErgebnis.Fehlgeschlagen)];
        });
        fabrik.Zeit.Setze(Jetzt.AddHours(2));

        var antwort = await Client(Rolle.Benutzer).PostJsonAsync(
            Route(ApiRouten.Belege.Bestaetigen, beleg.Id), new BestaetigenDto("Differenz ist Rundung des Lieferanten"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var gespeichert = await LadenAsync(beleg.Id);
        gespeichert.Tags.ShouldNotContain(t => t.TagId == SystemIds.TagZuPruefen);
        gespeichert.Befunde.ShouldHaveSingleItem().Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen);
        gespeichert.BestaetigtAm.ShouldBe(Jetzt.AddHours(2));
        gespeichert.BestaetigtVonId.ShouldBe(ApiFabrik.BenutzerId(Rolle.Benutzer));
        (await KorrespondentAsync(korrespondent.Id)).Geprueft.ShouldBeTrue();
        var audit = await AuditAsync(beleg.Id);
        audit.ShouldHaveSingleItem().NeuerWert.ShouldBe("Differenz ist Rundung des Lieferanten");
    }

    [Fact]
    public async Task Bestaetigen_ohne_Tag_aendert_nichts()
    {
        var beleg = await BelegAsync();

        var antwort = await Client().PostJsonAsync(Route(ApiRouten.Belege.Bestaetigen, beleg.Id), new BestaetigenDto(null));

        antwort.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LadenAsync(beleg.Id)).BestaetigtAm.ShouldBeNull();
        (await AuditAsync(beleg.Id)).ShouldBeEmpty();
    }

    // ---------- Tags ----------

    [Fact]
    public async Task Zu_pruefen_laesst_sich_nicht_per_Tag_Endpunkt_setzen_oder_entfernen()
    {
        var mitTag = await BelegAsync(b => b.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }]);
        var ohneTag = await BelegAsync();

        var entfernen = await Client().DeleteAsync(Route(ApiRouten.Belege.Tag, mitTag.Id, SystemIds.TagZuPruefen));
        var setzen = await Client().PostJsonAsync(Route(ApiRouten.Belege.Tags, ohneTag.Id), new TagZuordnenDto(SystemIds.TagZuPruefen));

        entfernen.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        setzen.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LadenAsync(mitTag.Id)).Tags.ShouldContain(t => t.TagId == SystemIds.TagZuPruefen);
        (await LadenAsync(ohneTag.Id)).Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Zahlungs_Tag_setzen_und_entfernen()
    {
        var beleg = await BelegAsync();
        var client = Client(Rolle.Benutzer);

        (await client.PostJsonAsync(Route(ApiRouten.Belege.Tags, beleg.Id), new TagZuordnenDto(_zahlungsTagId)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostJsonAsync(Route(ApiRouten.Belege.Tags, beleg.Id), new TagZuordnenDto(_zahlungsTagId)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tag = (await LadenAsync(beleg.Id)).Tags.ShouldHaveSingleItem();
        tag.TagId.ShouldBe(_zahlungsTagId);
        tag.GesetztVonId.ShouldBe(ApiFabrik.BenutzerId(Rolle.Benutzer));
        tag.GesetztAm.ShouldBe(Jetzt);

        (await client.DeleteAsync(Route(ApiRouten.Belege.Tag, beleg.Id, _zahlungsTagId))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LadenAsync(beleg.Id)).Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unbekannter_Tag_ergibt_400_und_unbekannter_Beleg_404()
    {
        var beleg = await BelegAsync();

        (await Client().PostJsonAsync(Route(ApiRouten.Belege.Tags, beleg.Id), new TagZuordnenDto(Guid.NewGuid())))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client().PostJsonAsync(Route(ApiRouten.Belege.Tags, Guid.NewGuid()), new TagZuordnenDto(_zahlungsTagId)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, Guid.NewGuid()), Aenderung(beleg)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------- Massenaktionen ----------

    [Fact]
    public async Task Massenaktion_Tags_setzen_zaehlt_nur_geaenderte_Belege()
    {
        var bereits = await BelegAsync(b => b.Tags = [new BelegTag { TagId = _zahlungsTagId, GesetztAm = Jetzt }]);
        var a = await BelegAsync();
        var b = await BelegAsync();

        var geaendert = await MassenaktionAsync(new([bereits.Id, a.Id, b.Id], MassenaktionArt.TagsSetzen, [_zahlungsTagId], null));

        geaendert.ShouldBe(2);
        foreach (var id in new[] { bereits.Id, a.Id, b.Id })
        {
            (await LadenAsync(id)).Tags.ShouldHaveSingleItem().TagId.ShouldBe(_zahlungsTagId);
        }
    }

    [Fact]
    public async Task Massenaktion_Tags_entfernen_zaehlt_nur_geaenderte_Belege()
    {
        var a = await BelegAsync(x => x.Tags = [new BelegTag { TagId = _zahlungsTagId, GesetztAm = Jetzt }]);
        var b = await BelegAsync(x => x.Tags = [new BelegTag { TagId = _zahlungsTagId, GesetztAm = Jetzt }]);
        var ohne = await BelegAsync();

        var geaendert = await MassenaktionAsync(new([a.Id, b.Id, ohne.Id], MassenaktionArt.TagsEntfernen, [_zahlungsTagId], null));

        geaendert.ShouldBe(2);
        (await LadenAsync(a.Id)).Tags.ShouldBeEmpty();
        (await LadenAsync(b.Id)).Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task Massenaktion_Kategorie_setzen_markiert_die_Kategorie_als_manuell()
    {
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var bereits = await BelegAsync(b => (b.KategorieId, b.KategorieManuell) = (kategorie.Id, true));
        var automatisch = await BelegAsync(b => b.KategorieId = kategorie.Id);
        var ohne = await BelegAsync();

        var geaendert = await MassenaktionAsync(new([bereits.Id, automatisch.Id, ohne.Id], MassenaktionArt.KategorieSetzen, null, kategorie.Id));

        geaendert.ShouldBe(2);
        foreach (var id in new[] { bereits.Id, automatisch.Id, ohne.Id })
        {
            var beleg = await LadenAsync(id);
            beleg.KategorieId.ShouldBe(kategorie.Id);
            beleg.KategorieManuell.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Massenaktion_Bestaetigen_entfernt_zu_pruefen()
    {
        var a = await BelegAsync(x => x.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }]);
        var b = await BelegAsync(x => x.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }]);
        var ohne = await BelegAsync();

        var geaendert = await MassenaktionAsync(new([a.Id, b.Id, ohne.Id], MassenaktionArt.Bestaetigen, null, null));

        geaendert.ShouldBe(2);
        foreach (var id in new[] { a.Id, b.Id })
        {
            var beleg = await LadenAsync(id);
            beleg.Tags.ShouldBeEmpty();
            beleg.BestaetigtAm.ShouldBe(Jetzt);
            (await AuditAsync(id)).ShouldHaveSingleItem();
        }

        (await LadenAsync(ohne.Id)).BestaetigtAm.ShouldBeNull();
    }

    [Fact]
    public async Task Massenaktion_ist_atomar_und_lehnt_ungueltige_Auftraege_ab()
    {
        var a = await BelegAsync();
        var b = await BelegAsync();
        var client = Client();

        // Ein unbekannter Beleg verhindert die ganze Aktion.
        (await client.PostJsonAsync(ApiRouten.Belege.Massenaktion,
            new MassenaktionDto([a.Id, b.Id, Guid.NewGuid()], MassenaktionArt.TagsSetzen, [_zahlungsTagId], null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // Ein Beleg in Verarbeitung ebenso.
        var inVerarbeitung = await BelegAsync(x => x.Status = BelegStatus.InVerarbeitung);
        (await client.PostJsonAsync(ApiRouten.Belege.Massenaktion,
            new MassenaktionDto([a.Id, inVerarbeitung.Id], MassenaktionArt.TagsSetzen, [_zahlungsTagId], null)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await LadenAsync(a.Id)).Tags.ShouldBeEmpty();
        (await LadenAsync(b.Id)).Tags.ShouldBeEmpty();

        (await client.PostJsonAsync(ApiRouten.Belege.Massenaktion,
            new MassenaktionDto([a.Id], MassenaktionArt.TagsEntfernen, [SystemIds.TagZuPruefen], null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostJsonAsync(ApiRouten.Belege.Massenaktion,
            new MassenaktionDto([a.Id], MassenaktionArt.KategorieSetzen, null, null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostJsonAsync(ApiRouten.Belege.Massenaktion,
            new MassenaktionDto([], MassenaktionArt.Bestaetigen, null, null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var zuViele = Enumerable.Range(0, BelegBearbeitungsDienst.MaxMassenaktion + 1).Select(_ => Guid.NewGuid()).ToArray();
        (await client.PostJsonAsync(ApiRouten.Belege.Massenaktion,
            new MassenaktionDto(zuViele, MassenaktionArt.Bestaetigen, null, null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------- Dublette ----------

    [Fact]
    public async Task Dublette_setzen_und_aufheben()
    {
        var original = await BelegAsync();
        var kopie = await BelegAsync();
        var route = Route(ApiRouten.Belege.Dublette, kopie.Id);

        (await Client().PostJsonAsync(route, new DubletteSetzenDto(original.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LadenAsync(kopie.Id)).DuplikatVonId.ShouldBe(original.Id);

        (await Client().PostJsonAsync(route, new DubletteSetzenDto(null))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await LadenAsync(kopie.Id)).DuplikatVonId.ShouldBeNull();
    }

    [Fact]
    public async Task Dublette_auf_sich_selbst_oder_auf_eine_Dublette_ergibt_400()
    {
        var original = await BelegAsync();
        var dublette = await BelegAsync(b => b.DuplikatVonId = original.Id);
        var dritter = await BelegAsync();

        (await Client().PostJsonAsync(Route(ApiRouten.Belege.Dublette, dritter.Id), new DubletteSetzenDto(dritter.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Client().PostJsonAsync(Route(ApiRouten.Belege.Dublette, dritter.Id), new DubletteSetzenDto(dublette.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await LadenAsync(dritter.Id)).DuplikatVonId.ShouldBeNull();
    }

    // ---------- Erneut auslesen ----------

    [Fact]
    public async Task Erneut_auslesen_ruft_den_Starter_und_ergibt_409_waehrend_der_Verarbeitung()
    {
        var starter = new FakeStarter();
        var f = fabrik.MitDiensten(d =>
        {
            d.RemoveAll<IVerarbeitungsStarter>();
            d.AddSingleton<IVerarbeitungsStarter>(starter);
        });
        var fertig = await BelegAsync();
        var inVerarbeitung = await BelegAsync(b => b.Status = BelegStatus.InVerarbeitung);
        var client = ClientFuer(f, Rolle.Benutzer);

        (await client.PostAsync(Route(ApiRouten.Belege.ErneutAuslesen, fertig.Id), null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await client.PostAsync(Route(ApiRouten.Belege.ErneutAuslesen, inVerarbeitung.Id), null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsync(Route(ApiRouten.Belege.ErneutAuslesen, Guid.NewGuid()), null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        starter.Aufrufe.ShouldBe([fertig.Id]);
    }

    [Fact]
    public async Task Beleg_in_Verarbeitung_ist_nicht_aenderbar()
    {
        var beleg = await BelegAsync(b => b.Status = BelegStatus.InVerarbeitung);

        (await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), Aenderung(beleg)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Client().PostJsonAsync(Route(ApiRouten.Belege.Tags, beleg.Id), new TagZuordnenDto(_zahlungsTagId)))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ---------- Rechte und Original ----------

    [Fact]
    public async Task NurLesen_bekommt_403_auf_allen_Endpunkten()
    {
        var beleg = await BelegAsync();
        var client = Client(Rolle.NurLesen);

        HttpResponseMessage[] antworten =
        [
            await client.PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), Aenderung(beleg)),
            await client.PutJsonAsync(Route(ApiRouten.Belege.Positionen, beleg.Id), Array.Empty<PositionAendernDto>()),
            await client.PostJsonAsync(Route(ApiRouten.Belege.Tags, beleg.Id), new TagZuordnenDto(_zahlungsTagId)),
            await client.DeleteAsync(Route(ApiRouten.Belege.Tag, beleg.Id, _zahlungsTagId)),
            await client.PostJsonAsync(Route(ApiRouten.Belege.Bestaetigen, beleg.Id), new BestaetigenDto(null)),
            await client.PostAsync(Route(ApiRouten.Belege.ErneutAuslesen, beleg.Id), null),
            await client.PostJsonAsync(Route(ApiRouten.Belege.Dublette, beleg.Id), new DubletteSetzenDto(null)),
            await client.PostJsonAsync(ApiRouten.Belege.Massenaktion, new MassenaktionDto([beleg.Id], MassenaktionArt.Bestaetigen, null, null)),
        ];

        antworten.Select(a => a.StatusCode).ShouldAllBe(s => s == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Die_Datei_in_der_Ablage_ist_nach_allen_Aenderungen_byte_gleich()
    {
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var anderer = await BelegAsync();
        var beleg = await BelegAsync(b => b.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }]);
        var (_, inhalt) = await AblageDateiAsync(beleg.Id, $"2026/test/{Guid.NewGuid():N}.pdf");
        var client = Client();

        (await client.PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id),
            Aenderung(beleg) with { KorrespondentId = korrespondent.Id, KategorieId = kategorie.Id, Titel = "Neu" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var relativ = (await LadenAsync(beleg.Id)).Dateien.ShouldHaveSingleItem().Pfad;
        var pfad = Path.Combine(fabrik.TempOrdner, "ablage", relativ);
        (await client.PutJsonAsync(Route(ApiRouten.Belege.Positionen, beleg.Id),
            new PositionAendernDto[] { new(1, "Papier", 1m, "Stk", 100m, 100m, 19m) })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostJsonAsync(Route(ApiRouten.Belege.Tags, beleg.Id), new TagZuordnenDto(_zahlungsTagId)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostJsonAsync(Route(ApiRouten.Belege.Dublette, beleg.Id), new DubletteSetzenDto(anderer.Id)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostJsonAsync(Route(ApiRouten.Belege.Bestaetigen, beleg.Id), new BestaetigenDto("passt")))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.PostJsonAsync(ApiRouten.Belege.Massenaktion,
            new MassenaktionDto([beleg.Id], MassenaktionArt.TagsEntfernen, [_zahlungsTagId], null))).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await File.ReadAllBytesAsync(pfad)).ShouldBe(inhalt);
        var datei = (await LadenAsync(beleg.Id)).Dateien.ShouldHaveSingleItem();
        datei.Pfad.ShouldBe(relativ);
    }

    // ---------- Ablage folgt dem Korrespondenten, solange „zu prüfen“ ----------

    [Fact]
    public async Task Korrespondent_gewechselt_bei_zu_pruefen_verschiebt_Original_und_XML_in_den_neuen_Ordner()
    {
        var neu = await fabrik.SpeichernAsync(Testdaten.Korrespondent($"Bürowelt {Guid.NewGuid():N}"[..20]));
        var beleg = await BelegAsync(b =>
        {
            b.Rechnungsnummer = "RE-4711";
            b.Rechnungsdatum = new DateOnly(2026, 3, 9);
            b.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }];
        });
        var alterOrdner = $"2026/_Unbekannt_{Guid.NewGuid():N}";
        var (altPdf, pdf) = await AblageDateiAsync(beleg.Id, $"{alterOrdner}/2026-03-09_RE-4711.pdf");
        var (altXml, xml) = await AblageDateiAsync(beleg.Id, $"{alterOrdner}/2026-03-09_RE-4711.xml", DateiRolle.EingebettetesXml);

        (await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), Aenderung(beleg) with { KorrespondentId = neu.Id }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var ordner = $"2026/{neu.Name.Replace("ü", "ue").Replace(' ', '_')}";
        var dateien = (await LadenAsync(beleg.Id)).Dateien;
        var original = dateien.Single(d => d.Rolle == DateiRolle.Original);
        var eingebettet = dateien.Single(d => d.Rolle == DateiRolle.EingebettetesXml);
        original.Pfad.ShouldBe($"{ordner}/2026-03-09_RE-4711.pdf");
        eingebettet.Pfad.ShouldBe($"{ordner}/2026-03-09_RE-4711.xml");
        original.Sha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(pdf)));

        var ablage = Path.Combine(fabrik.TempOrdner, "ablage");
        (await File.ReadAllBytesAsync(Path.Combine(ablage, original.Pfad))).ShouldBe(pdf);
        (await File.ReadAllBytesAsync(Path.Combine(ablage, eingebettet.Pfad))).ShouldBe(xml);
        File.Exists(altPdf).ShouldBeFalse();
        File.Exists(altXml).ShouldBeFalse();
        Directory.Exists(Path.Combine(ablage, alterOrdner)).ShouldBeFalse();
        if (OperatingSystem.IsWindows())
        {
            File.GetAttributes(Path.Combine(ablage, original.Pfad)).HasFlag(FileAttributes.ReadOnly).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Verschieben_haengt_bei_belegtem_Zielpfad_ein_Suffix_an()
    {
        var neu = await fabrik.SpeichernAsync(Testdaten.Korrespondent($"Ziel {Guid.NewGuid():N}"));
        var beleg = await BelegAsync(b =>
        {
            b.Rechnungsnummer = "RE-1";
            b.Rechnungsdatum = new DateOnly(2026, 4, 1);
            b.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }];
        });
        var ordner = $"2026/{neu.Name.Replace(' ', '_')}";
        var belegt = Path.Combine(fabrik.TempOrdner, "ablage", ordner, "2026-04-01_RE-1.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(belegt)!);
        await File.WriteAllBytesAsync(belegt, [1, 2, 3]);
        await AblageDateiAsync(beleg.Id, $"2026/alt_{Guid.NewGuid():N}/2026-04-01_RE-1.pdf");

        (await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), Aenderung(beleg) with { KorrespondentId = neu.Id }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await LadenAsync(beleg.Id)).Dateien.ShouldHaveSingleItem().Pfad.ShouldBe($"{ordner}/2026-04-01_RE-1_2.pdf");
        (await File.ReadAllBytesAsync(belegt)).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Bestaetigter_Beleg_bleibt_beim_Korrespondentenwechsel_am_alten_Ort()
    {
        var neu = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var beleg = await BelegAsync();
        var relativ = $"2026/alt_{Guid.NewGuid():N}/datei.pdf";
        var (pfad, inhalt) = await AblageDateiAsync(beleg.Id, relativ);

        (await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), Aenderung(beleg) with { KorrespondentId = neu.Id }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await LadenAsync(beleg.Id)).Dateien.ShouldHaveSingleItem().Pfad.ShouldBe(relativ);
        (await File.ReadAllBytesAsync(pfad)).ShouldBe(inhalt);
    }

    [Fact]
    public async Task Ohne_Korrespondentenwechsel_bleibt_die_Datei_auch_bei_zu_pruefen_liegen()
    {
        var beleg = await BelegAsync(b => b.Tags = [new BelegTag { TagId = SystemIds.TagZuPruefen, GesetztAm = Jetzt }]);
        var relativ = $"2026/alt_{Guid.NewGuid():N}/datei.pdf";
        await AblageDateiAsync(beleg.Id, relativ);

        (await Client().PutJsonAsync(Route(ApiRouten.Belege.Einzeln, beleg.Id), Aenderung(beleg) with { Titel = "Neu" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await LadenAsync(beleg.Id)).Dateien.ShouldHaveSingleItem().Pfad.ShouldBe(relativ);
    }

    // ---------- Hilfen ----------

    /// <summary>Legt eine schreibgeschützte Ablage-Datei mit Zufallsinhalt an und speichert die <see cref="BelegDatei"/>.</summary>
    private async Task<(string Pfad, byte[] Inhalt)> AblageDateiAsync(Guid belegId, string relativ, DateiRolle rolle = DateiRolle.Original)
    {
        var pfad = Path.Combine(fabrik.TempOrdner, "ablage", relativ);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        var inhalt = RandomNumberGenerator.GetBytes(4096);
        await File.WriteAllBytesAsync(pfad, inhalt);
        File.SetAttributes(pfad, File.GetAttributes(pfad) | FileAttributes.ReadOnly);
        await fabrik.SpeichernAsync(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = belegId,
            Rolle = rolle,
            Pfad = relativ,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(inhalt)),
            MimeType = rolle == DateiRolle.Original ? "application/pdf" : "application/xml",
            Groesse = inhalt.Length,
            AbgelegtAm = Jetzt,
        });
        return (pfad, inhalt);
    }

    private HttpClient Client(Rolle rolle = Rolle.Admin) => ClientFuer(fabrik, rolle);

    private static HttpClient ClientFuer(ApiFabrik f, Rolle rolle) => f.ErzeugeClient(rolle);

    private static string Route(string vorlage, Guid id, Guid? tagId = null)
    {
        var route = vorlage.Replace("{id}", id.ToString(), StringComparison.Ordinal);
        return tagId is null ? route : route.Replace("{tagId}", tagId.ToString(), StringComparison.Ordinal);
    }

    private async Task<int> MassenaktionAsync(MassenaktionDto aktion)
    {
        var antwort = await Client(Rolle.Benutzer).PostJsonAsync(ApiRouten.Belege.Massenaktion, aktion);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await antwort.Content.LesenAsync<AnzahlGeaendertDto>())!.Geaendert;
    }

    private async Task<Beleg> BelegAsync(Action<Beleg>? anpassen = null)
    {
        var beleg = Testdaten.Beleg();
        anpassen?.Invoke(beleg);
        return await fabrik.SpeichernAsync(beleg);
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
        [.. b.Steuerzeilen.Select(z => new SteuerzeileAendernDto(z.Satz, z.Netto, z.Steuer))]);

    private static Position Position(int nr, string text, decimal gesamt) =>
        new() { Id = Guid.NewGuid(), Nr = nr, Text = text, Menge = 1, Einzelpreis = gesamt, Gesamt = gesamt, Steuersatz = 19 };

    private static PruefBefund Befund(PruefungArt art, PruefErgebnis ergebnis) =>
        new() { Id = Guid.NewGuid(), Pruefung = art, Ergebnis = ergebnis, Detail = "Test", Zeitpunkt = Jetzt };

    private async Task<Beleg> LadenAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Belege.AsNoTracking()
            .Include(b => b.Steuerzeilen)
            .Include(b => b.Positionen)
            .Include(b => b.Tags)
            .Include(b => b.Befunde)
            .Include(b => b.Dateien)
            .SingleAsync(b => b.Id == id);
    }

    private async Task<Korrespondent> KorrespondentAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Korrespondenten.AsNoTracking().SingleAsync(k => k.Id == id);
    }

    private async Task<List<AuditEintrag>> AuditAsync(Guid belegId)
    {
        using var scope = fabrik.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().AuditEintraege.AsNoTracking()
            .Where(a => a.BelegId == belegId && a.Aktion == BelegBearbeitungsDienst.AuditAktionBestaetigt)
            .ToListAsync();
    }

    private sealed class FakeStarter : IVerarbeitungsStarter
    {
        public List<Guid> Aufrufe { get; } = [];

        public Task ErneutVerarbeitenAsync(Guid belegId, CancellationToken ct)
        {
            Aufrufe.Add(belegId);
            return Task.CompletedTask;
        }
    }
}
