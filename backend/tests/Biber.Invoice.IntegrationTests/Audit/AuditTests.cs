using System.Net;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.Audit;

[Collection("Api")]
public sealed class AuditTests(ApiFabrik fabrik)
{
    private static readonly Guid LehmannId = Guid.NewGuid();

    [Fact]
    public async Task Kategorie_und_Leistungsdatum_aendern_erzeugt_zwei_Eintraege_mit_Benutzer()
    {
        var alt = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var neu = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(kategorieId: alt.Id));
        await using var alsLehmann = AlsBenutzer();

        await AendernAsync(alsLehmann, beleg.Id, b =>
        {
            b.KategorieId = neu.Id;
            b.Leistungsdatum = new DateOnly(2026, 11, 19);
        });

        var eintraege = await EintraegeAsync(beleg.Id, AuditAktion.Geaendert);
        eintraege.Count.ShouldBe(2);
        eintraege.ShouldAllBe(a => a.BenutzerName == "A. Lehmann" && a.BenutzerId == LehmannId);
        var kategorie = eintraege.Single(a => a.Feld == nameof(Beleg.KategorieId));
        kategorie.AlterWert.ShouldBe(alt.Id.ToString());
        kategorie.NeuerWert.ShouldBe(neu.Id.ToString());
        var datum = eintraege.Single(a => a.Feld == nameof(Beleg.Leistungsdatum));
        datum.AlterWert.ShouldBeNull();
        datum.NeuerWert.ShouldBe("2026-11-19");
        datum.Zeitpunkt.ShouldBe(alsLehmann.Zeit.GetUtcNow());
    }

    [Fact]
    public async Task Aenderung_ohne_Http_Kontext_ist_System()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());

        await AendernAsync(fabrik, beleg.Id, b => b.Titel = "Neu");

        var eintrag = (await EintraegeAsync(beleg.Id, AuditAktion.Geaendert)).Single();
        eintrag.BenutzerName.ShouldBe("System");
        eintrag.BenutzerId.ShouldBeNull();
        (await EintraegeAsync(beleg.Id, AuditAktion.Angelegt)).Single().Entitaet.ShouldBe(nameof(Beleg));
    }

    [Fact]
    public async Task Tag_setzen_und_entfernen_wird_mit_Tag_Namen_protokolliert()
    {
        var tag = await fabrik.SpeichernAsync(Testdaten.Tag("Steuer 2026"));
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());

        await fabrik.SpeichernAsync(new BelegTag { BelegId = beleg.Id, TagId = tag.Id, GesetztAm = fabrik.Zeit.GetUtcNow() });
        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            db.BelegTags.Remove(await db.BelegTags.SingleAsync(t => t.BelegId == beleg.Id));
            await db.SaveChangesAsync();
        }

        var gesetzt = (await EintraegeAsync(beleg.Id, AuditAktion.Angelegt)).Single(a => a.Entitaet == nameof(BelegTag));
        gesetzt.NeuerWert.ShouldBe("Steuer 2026");
        var entfernt = (await EintraegeAsync(beleg.Id, AuditAktion.Geloescht)).Single();
        entfernt.Entitaet.ShouldBe(nameof(BelegTag));
        entfernt.AlterWert.ShouldBe("Steuer 2026");
    }

    [Fact]
    public async Task Position_hinzufuegen_wird_protokolliert()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());

        await fabrik.SpeichernAsync(new Position { Id = Guid.NewGuid(), BelegId = beleg.Id, Nr = 1, Text = "Kopierpapier A4" });

        var eintrag = (await EintraegeAsync(beleg.Id, AuditAktion.Angelegt)).Single(a => a.Entitaet == nameof(Position));
        eintrag.NeuerWert.ShouldBe("Kopierpapier A4");
        eintrag.BelegId.ShouldBe(beleg.Id);
    }

    [Fact]
    public async Task Stammdaten_aenderung_wird_protokolliert()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Alt GmbH"));

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Korrespondenten.SingleAsync(k => k.Id == korrespondent.Id)).Name = "Neu GmbH";
        await db.SaveChangesAsync();

        var eintrag = await db.AuditEintraege.SingleAsync(a =>
            a.EntitaetId == korrespondent.Id.ToString() && a.Aktion == AuditAktion.Geaendert);
        eintrag.Entitaet.ShouldBe(nameof(Korrespondent));
        eintrag.AlterWert.ShouldBe("Alt GmbH");
        eintrag.NeuerWert.ShouldBe("Neu GmbH");
    }

    [Fact]
    public async Task Volltext_aenderung_erzeugt_keinen_Eintrag()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());

        await AendernAsync(fabrik, beleg.Id, b => b.Volltext = "Neuer Volltext");

        (await EintraegeAsync(beleg.Id, AuditAktion.Geaendert)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Rollback_hinterlaesst_keine_Eintraege()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());

        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            await using var transaktion = await db.Database.BeginTransactionAsync();
            (await db.Belege.SingleAsync(b => b.Id == beleg.Id)).Titel = "Verworfen";
            await db.SaveChangesAsync();
            await transaktion.RollbackAsync();
        }

        (await EintraegeAsync(beleg.Id, AuditAktion.Geaendert)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Historie_liefert_die_Texte_des_Designs()
    {
        var sonstiges = await fabrik.SpeichernAsync(Testdaten.Kategorie("Sonstiges"));
        var buero = await fabrik.SpeichernAsync(Testdaten.Kategorie("Bürobedarf"));
        var lieferant = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Bürowelt Krämer KG"));
        var vorgang = await fabrik.SpeichernAsync(new ImportVorgang
        {
            Id = Guid.NewGuid(),
            Quelle = ImportQuelle.Ordner,
            Dateiname = "Scan_20261119_0812.pdf",
            ErstelltAm = fabrik.Zeit.GetUtcNow(),
        });
        var beleg = Testdaten.Beleg();
        beleg.Quelle = ImportQuelle.Ordner;
        beleg.ImportVorgangId = vorgang.Id;
        await fabrik.SpeichernAsync(beleg);

        // Pipeline (System): auslesen, zuordnen, Tags, ablegen.
        await AendernAsync(fabrik, beleg.Id, b =>
        {
            b.ExtraktionsQuelle = ExtraktionsQuelle.DocumentIntelligence;
            b.DiKonfidenz = 0.74m;
            b.ExtrahiertAm = fabrik.Zeit.GetUtcNow();
            b.KorrespondentId = lieferant.Id;
            b.KorrespondentErkennung = KorrespondentErkennung.UstIdNr;
            b.KategorieId = sonstiges.Id;
        });
        using (var scope = fabrik.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            db.BelegTags.Add(new BelegTag { BelegId = beleg.Id, TagId = SystemIds.TagInbox });
            db.BelegTags.Add(new BelegTag { BelegId = beleg.Id, TagId = SystemIds.TagZuPruefen });
            db.BelegDateien.Add(new BelegDatei
            {
                Id = Guid.NewGuid(),
                BelegId = beleg.Id,
                Rolle = DateiRolle.Original,
                Pfad = "2026/Buerowelt_Kraemer_KG/2026-11-19_2026-48817.pdf",
                Sha256 = "abc",
                MimeType = "application/pdf",
            });
            await db.SaveChangesAsync();
        }

        await using var alsLehmann = AlsBenutzer();
        alsLehmann.Zeit.Setze(fabrik.Zeit.GetUtcNow().AddDays(1));
        await AendernAsync(alsLehmann, beleg.Id, b =>
        {
            b.KategorieId = buero.Id;
            b.Leistungsdatum = new DateOnly(2026, 11, 19);
        });

        var historie = await HistorieAsync(beleg.Id);

        var texte = historie.Select(h => h.Text).ToList();
        texte.ShouldContain("Kategorie geändert");
        texte.ShouldContain("Leistungsdatum ergänzt");
        texte.ShouldContain("Abgelegt unter ablage/2026/Buerowelt_Kraemer_KG/2026-11-19_2026-48817.pdf");
        texte.ShouldContain("Tags gesetzt: Inbox, zu prüfen");
        texte.ShouldContain("Kategorie „Sonstiges“ gesetzt");
        texte.ShouldContain("Korrespondent zugeordnet: Bürowelt Krämer KG (über USt-IdNr.)");
        texte.ShouldContain("Ausgelesen mit Document Intelligence, 1 Seite, Konfidenz 0,74");
        texte[^1].ShouldBe("Importiert aus Eingangsordner: Scan_20261119_0812.pdf");

        var kategorie = historie.Single(h => h.Text == "Kategorie geändert");
        $"{kategorie.AlterWert} → {kategorie.NeuerWert}".ShouldBe("Sonstiges → Bürobedarf");
        kategorie.Wer.ShouldBe("A. Lehmann");
        kategorie.Art.ShouldBe("benutzer");
        historie[0].Zeitpunkt.ShouldBeGreaterThanOrEqualTo(historie[^1].Zeitpunkt);
        var datum = historie.Single(h => h.Text == "Leistungsdatum ergänzt");
        (datum.AlterWert, datum.NeuerWert).ShouldBe(("leer", "19.11.2026"));
        historie.Single(h => h.Text.StartsWith("Tags gesetzt", StringComparison.Ordinal)).Art.ShouldBe("warnung");
        historie.Single(h => h.Text.StartsWith("Abgelegt", StringComparison.Ordinal)).Art.ShouldBe("system");
    }

    [Fact]
    public async Task Historie_nennt_Auto_Matching_als_Erkennung()
    {
        var lieferant = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Hoster GmbH"));
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());

        await AendernAsync(fabrik, beleg.Id, b =>
        {
            b.KorrespondentId = lieferant.Id;
            b.KorrespondentErkennung = KorrespondentErkennung.Auto;
        });

        var texte = (await HistorieAsync(beleg.Id)).Select(h => h.Text).ToList();
        texte.ShouldContain("Korrespondent zugeordnet: Hoster GmbH (über Auto-Matching)");
    }

    [Fact]
    public async Task Historie_fuer_unbekannten_Beleg_ist_404()
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync($"/api/belege/{Guid.NewGuid()}/historie");

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Notiz_ueber_IAuditSchreiber_erscheint_in_der_Historie()
    {
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg());

        using (var scope = fabrik.ErzeugeScope())
        {
            scope.ServiceProvider.GetRequiredService<IAuditSchreiber>()
                .Notieren(nameof(Beleg), beleg.Id.ToString(), beleg.Id, "Exportiert", "Exportiert nach DATEV");
            await scope.ServiceProvider.GetRequiredService<BiberDbContext>().SaveChangesAsync();
        }

        var historie = await HistorieAsync(beleg.Id);
        historie[0].Text.ShouldBe("Exportiert nach DATEV");
        historie[0].Aktion.ShouldBe("Exportiert");
    }

    private ApiFabrik AlsBenutzer() => fabrik.MitDiensten(d =>
    {
        d.RemoveAll<IAktuellerBenutzer>();
        d.AddScoped<IAktuellerBenutzer, Lehmann>();
    });

    private static async Task AendernAsync(ApiFabrik f, Guid belegId, Action<Beleg> aendern)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        aendern(await db.Belege.SingleAsync(b => b.Id == belegId));
        await db.SaveChangesAsync();
    }

    private async Task<List<AuditEintrag>> EintraegeAsync(Guid belegId, string aktion)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.AuditEintraege.Where(a => a.BelegId == belegId && a.Aktion == aktion).ToListAsync();
    }

    private async Task<AuditEintragDto[]> HistorieAsync(Guid belegId)
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync($"/api/belege/{belegId}/historie");
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await antwort.Content.LesenAsync<AuditEintragDto[]>())!;
    }

    private static class AuditAktion
    {
        public const string Angelegt = "Angelegt";
        public const string Geaendert = "Geaendert";
        public const string Geloescht = "Geloescht";
    }

    private sealed class Lehmann : IAktuellerBenutzer
    {
        public Guid? Id => LehmannId;
        public string Anzeigename => "A. Lehmann";
        public bool IstSystem => false;
        public bool HatRolle(Rolle rolle) => true;
    }
}
