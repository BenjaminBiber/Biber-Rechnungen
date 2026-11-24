using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Positionen;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class PositionenSchrittTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Gleiche_Position_zweier_Belege_desselben_Korrespondenten_ergibt_einen_Artikel()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());

        var erste = await AusfuehrenAsync(k.Id, Pos("4711 Kaffee Crema 500 g", 6.99m, "Stück"));
        var zweite = await AusfuehrenAsync(k.Id, Pos("KAFFEE  CREMA 500 g.", 7.49m, "Stück"));

        var a = erste.Single();
        a.ArtikelId.ShouldNotBeNull();
        zweite.Single().ArtikelId.ShouldBe(a.ArtikelId);
        a.TextNormalisiert.ShouldBe("kaffee crema 500 g");
        a.Grundpreis.ShouldBe(13.98m);
        a.GrundpreisEinheit.ShouldBe("kg");
        zweite.Single().Grundpreis.ShouldBe(14.98m);

        var artikel = await ArtikelAsync(k.Id);
        artikel.Count.ShouldBe(1);
        artikel[0].Name.ShouldBe("4711 Kaffee Crema 500 g");
        artikel[0].NameNormalisiert.ShouldBe("kaffee crema 500 g");
        artikel[0].Einheit.ShouldBe("Stück");
    }

    [Fact]
    public async Task Anderer_Korrespondent_bekommt_eigenen_Artikel()
    {
        var k1 = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var k2 = await fabrik.SpeichernAsync(Testdaten.Korrespondent());

        var erste = await AusfuehrenAsync(k1.Id, Pos("Wasser 6 x 1,5 l", 4.50m));
        var zweite = await AusfuehrenAsync(k2.Id, Pos("Wasser 6 x 1,5 l", 4.50m));

        zweite.Single().ArtikelId.ShouldNotBe(erste.Single().ArtikelId);
        (await ArtikelAsync(k1.Id)).Count.ShouldBe(1);
        (await ArtikelAsync(k2.Id)).Count.ShouldBe(1);
        erste.Single().Grundpreis.ShouldBe(0.50m);
        erste.Single().GrundpreisEinheit.ShouldBe("l");
    }

    [Fact]
    public async Task Ohne_Korrespondent_entstehen_keine_Artikel()
    {
        var positionen = await AusfuehrenAsync(null, Pos("Kaffee 500 g", 6.99m));

        var p = positionen.Single();
        p.ArtikelId.ShouldBeNull();
        p.TextNormalisiert.ShouldBe("kaffee 500 g");
        p.Grundpreis.ShouldBe(13.98m);
    }

    [Fact]
    public async Task Position_ohne_Text_bleibt_ohne_Artikel()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());

        var p = (await AusfuehrenAsync(k.Id, Pos("  ", 1m))).Single();

        p.ArtikelId.ShouldBeNull();
        p.TextNormalisiert.ShouldBeNull();
        (await ArtikelAsync(k.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Gleiche_Texte_in_einem_Beleg_teilen_sich_einen_Artikel()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());

        var positionen = await AusfuehrenAsync(k.Id, Pos("Kugelschreiber", 0.99m, "Stück"), Pos("kugelschreiber", 0.89m, "Stück"));

        positionen.Select(p => p.ArtikelId).Distinct().Count().ShouldBe(1);
        positionen[0].Grundpreis.ShouldBe(0.99m);
        positionen[0].GrundpreisEinheit.ShouldBe("Stück");
        (await ArtikelAsync(k.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Stammartikel_bestimmt_den_Grundpreis()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var stamm = await fabrik.SpeichernAsync(new Stammartikel
        {
            Id = Guid.NewGuid(),
            Name = $"Kopierpapier {Guid.NewGuid():N}",
            Basiseinheit = "Blatt",
            GrundpreisMenge = 100,
        });
        await fabrik.SpeichernAsync(new Artikel
        {
            Id = Guid.NewGuid(),
            KorrespondentId = k.Id,
            Name = "Kopierpapier Karton",
            NameNormalisiert = "kopierpapier karton",
            StammartikelId = stamm.Id,
            UmrechnungFaktor = 2500,
        });

        var p = (await AusfuehrenAsync(k.Id, Pos("Kopierpapier Karton", 24.90m, "Karton"))).Single();

        p.Grundpreis.ShouldBe(1.00m);
        p.GrundpreisEinheit.ShouldBe("100 Blatt");
        (await ArtikelAsync(k.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Zweiter_Lauf_erzeugt_keine_Dubletten()
    {
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var beleg = Testdaten.Beleg(korrespondentId: k.Id);
        beleg.Positionen = [Pos("Toner schwarz", 59.00m, "Stück"), Pos("Papier 500 Blatt", 4.99m, "Stück")];
        await fabrik.SpeichernAsync(beleg);

        var erster = await AusfuehrenAsync(beleg.Id);
        var zweiter = await AusfuehrenAsync(beleg.Id);

        zweiter.Select(p => p.ArtikelId).ShouldBe(erster.Select(p => p.ArtikelId));
        zweiter.Select(p => p.Grundpreis).ShouldBe(erster.Select(p => p.Grundpreis));
        (await ArtikelAsync(k.Id)).Count.ShouldBe(2);
    }

    private async Task<List<Position>> AusfuehrenAsync(Guid? korrespondentId, params Position[] positionen)
    {
        var beleg = Testdaten.Beleg(korrespondentId: korrespondentId);
        beleg.Positionen = [.. positionen];
        await fabrik.SpeichernAsync(beleg);
        return await AusfuehrenAsync(beleg.Id);
    }

    private async Task<List<Position>> AusfuehrenAsync(Guid belegId)
    {
        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<PositionenSchritt>().Single();
            var beleg = await db.Belege.Include(b => b.Positionen).SingleAsync(b => b.Id == belegId);
            var kontext = new VerarbeitungsKontext
            {
                Vorgang = new ImportVorgang { Id = Guid.NewGuid(), Dateiname = "beleg.pdf" },
                Beleg = beleg,
                DateiPfad = "beleg.pdf",
                Dateiname = "beleg.pdf",
            };

            (await schritt.AusfuehrenAsync(kontext, CancellationToken.None)).ShouldBe(SchrittErgebnis.Weiter);
            await db.SaveChangesAsync();
        }

        using var lesen = fabrik.ErzeugeScope();
        var lesenDb = lesen.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await lesenDb.Set<Position>().AsNoTracking().Where(p => p.BelegId == belegId).OrderBy(p => p.Nr).ToListAsync();
    }

    private async Task<List<Artikel>> ArtikelAsync(Guid korrespondentId)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Artikel.AsNoTracking().Where(a => a.KorrespondentId == korrespondentId).ToListAsync();
    }

    private static int naechsteNr;

    private static Position Pos(string text, decimal einzelpreis, string? einheit = null) => new()
    {
        Id = Guid.NewGuid(),
        Nr = Interlocked.Increment(ref naechsteNr),
        Text = text,
        Menge = 1,
        Einheit = einheit,
        Einzelpreis = einzelpreis,
        Gesamt = einzelpreis,
    };
}
