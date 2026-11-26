using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Biber.Invoice.IntegrationTests.Persistenz;

[Collection("Api")]
public sealed class SchemaTests(ApiFabrik fabrik)
{
    private static readonly DateTimeOffset Jetzt = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Beleg_mit_allen_Teilen_wird_gespeichert_und_geladen()
    {
        var benutzer = await fabrik.SpeichernAsync(Testdaten.Benutzer());
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var tag = await fabrik.SpeichernAsync(Testdaten.Tag());
        var artikel = await fabrik.SpeichernAsync(new Artikel
        {
            Id = Guid.NewGuid(),
            KorrespondentId = korrespondent.Id,
            Name = "Toner TN-2420",
            NameNormalisiert = "toner tn 2420",
        });
        var beleg = Testdaten.Beleg(korrespondentId: korrespondent.Id, kategorieId: kategorie.Id, brutto: 119m);
        beleg.DiKonfidenz = 0.9731m;
        beleg.BestaetigtVonId = benutzer.Id;
        beleg.Dateien.Add(new BelegDatei
        {
            Id = Guid.NewGuid(),
            Rolle = DateiRolle.Original,
            Pfad = "2026/01/re.pdf",
            Sha256 = new string('a', 64),
            Groesse = 12345,
            MimeType = "application/pdf",
            AbgelegtAm = Jetzt,
            PruefErgebnis = true,
        });
        beleg.Steuerzeilen.Add(new Steuerzeile { Id = Guid.NewGuid(), Satz = 19m, Netto = 100m, Steuer = 19m });
        beleg.Positionen.Add(new Position
        {
            Id = Guid.NewGuid(),
            Nr = 1,
            Text = "Toner TN-2420",
            Menge = 2m,
            Einheit = "Stk",
            Einzelpreis = 0.0125m,
            Gesamt = 100m,
            Steuersatz = 19m,
            ArtikelId = artikel.Id,
        });
        beleg.Tags.Add(new BelegTag { TagId = tag.Id, GesetztAm = Jetzt, GesetztVonId = benutzer.Id });
        beleg.Befunde.Add(new PruefBefund
        {
            Id = Guid.NewGuid(),
            Pruefung = PruefungArt.NettoSteuerBrutto,
            Ergebnis = PruefErgebnis.Ok,
            Detail = "passt",
            Zeitpunkt = Jetzt,
        });
        await fabrik.SpeichernAsync(beleg);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var geladen = await db.Belege
            .Include(b => b.Dateien).Include(b => b.Steuerzeilen).Include(b => b.Positionen).ThenInclude(p => p.Artikel)
            .Include(b => b.Tags).ThenInclude(t => t.Tag).Include(b => b.Befunde)
            .Include(b => b.Korrespondent).Include(b => b.Kategorie)
            .SingleAsync(b => b.Id == beleg.Id);

        geladen.Waehrung.ShouldBe("EUR");
        geladen.Brutto.ShouldBe(119m);
        geladen.DiKonfidenz.ShouldBe(0.9731m);
        geladen.Status.ShouldBe(BelegStatus.Fertig);
        geladen.Korrespondent!.Name.ShouldBe(korrespondent.Name);
        geladen.Kategorie!.Name.ShouldBe(kategorie.Name);
        geladen.Dateien.ShouldHaveSingleItem().Sha256.ShouldBe(new string('a', 64));
        geladen.Steuerzeilen.ShouldHaveSingleItem().Satz.ShouldBe(19m);
        geladen.Positionen.ShouldHaveSingleItem().Einzelpreis.ShouldBe(0.0125m);
        geladen.Positionen[0].Artikel!.Name.ShouldBe("Toner TN-2420");
        geladen.Tags.ShouldHaveSingleItem().Tag!.Name.ShouldBe(tag.Name);
        geladen.Befunde.ShouldHaveSingleItem().Pruefung.ShouldBe(PruefungArt.NettoSteuerBrutto);

        var status = await db.Database
            .SqlQuery<string>($"SELECT status AS \"Value\" FROM beleg WHERE id = {beleg.Id}").SingleAsync();
        status.ShouldBe("Fertig");
    }

    [Fact]
    public async Task Loeschen_eines_Belegs_kaskadiert_auf_seine_Teile()
    {
        var tag = await fabrik.SpeichernAsync(Testdaten.Tag());
        var beleg = Testdaten.Beleg();
        beleg.Steuerzeilen.Add(new Steuerzeile { Id = Guid.NewGuid(), Satz = 7m, Netto = 10m, Steuer = 0.7m });
        beleg.Positionen.Add(new Position { Id = Guid.NewGuid(), Nr = 1, Text = "Buch" });
        beleg.Tags.Add(new BelegTag { TagId = tag.Id, GesetztAm = Jetzt });
        await fabrik.SpeichernAsync(beleg);

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        await db.Belege.Where(b => b.Id == beleg.Id).ExecuteDeleteAsync();

        (await db.Steuerzeilen.AnyAsync(s => s.BelegId == beleg.Id)).ShouldBeFalse();
        (await db.Positionen.AnyAsync(p => p.BelegId == beleg.Id)).ShouldBeFalse();
        (await db.BelegTags.AnyAsync(t => t.BelegId == beleg.Id)).ShouldBeFalse();
        (await db.Tags.AnyAsync(t => t.Id == tag.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Korrespondent_mit_Listen_Anschrift_und_Matching()
    {
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie());
        var korrespondent = Testdaten.Korrespondent(ustIdNr: "DE123456789");
        korrespondent.Aliase = ["Krämer", "Kraemer KG"];
        korrespondent.Bankverbindungen = [new Bankverbindung("DE02120300000000202051", "BYLADEM1001"), new Bankverbindung("DE89370400440532013000")];
        korrespondent.Anschrift = new Anschrift { Strasse = "Hauptstr. 1", Plz = "53111", Ort = "Bonn", Land = "DE" };
        korrespondent.Matching = new Matching { Algorithmus = MatchingAlgorithmus.Exakt, Muster = "Krämer", GrossKlein = true };
        korrespondent.LogoQuelle = LogoQuelle.Favicon;
        korrespondent.DefaultKategorieId = kategorie.Id;
        await fabrik.SpeichernAsync(korrespondent);

        var geladen = await LadenAsync<Korrespondent>(k => k.Id == korrespondent.Id);

        geladen.Aliase.ShouldBe(["Krämer", "Kraemer KG"]);
        geladen.Bankverbindungen.ShouldBe([new Bankverbindung("DE02120300000000202051", "BYLADEM1001"), new Bankverbindung("DE89370400440532013000")]);
        geladen.Anschrift.Ort.ShouldBe("Bonn");
        geladen.Matching.Algorithmus.ShouldBe(MatchingAlgorithmus.Exakt);
        geladen.Matching.Muster.ShouldBe("Krämer");
        geladen.Matching.GrossKlein.ShouldBeTrue();
        geladen.DefaultKategorieId.ShouldBe(kategorie.Id);
    }

    [Fact]
    public async Task Kategorie_und_Tag_mit_Matching()
    {
        var kategorie = Testdaten.Kategorie();
        kategorie.EuerZeile = "25";
        kategorie.Matching = new Matching { Algorithmus = MatchingAlgorithmus.EinWort, Muster = "toner papier" };
        var tag = Testdaten.Tag(farbe: "petrol");
        tag.IstZahlungsTag = true;
        tag.Matching = new Matching { Algorithmus = MatchingAlgorithmus.Regex, Muster = "^RE-" };
        await fabrik.SpeichernAsync(kategorie);
        await fabrik.SpeichernAsync(tag);

        var kategorieGeladen = await LadenAsync<Kategorie>(k => k.Id == kategorie.Id);
        var tagGeladen = await LadenAsync<Tag>(t => t.Id == tag.Id);

        kategorieGeladen.EuerZeile.ShouldBe("25");
        kategorieGeladen.Matching.Muster.ShouldBe("toner papier");
        tagGeladen.Farbe.ShouldBe("petrol");
        tagGeladen.IstZahlungsTag.ShouldBeTrue();
        tagGeladen.Matching.Algorithmus.ShouldBe(MatchingAlgorithmus.Regex);
    }

    [Fact]
    public async Task Artikel_mit_Stammartikel()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var stamm = await fabrik.SpeichernAsync(new Stammartikel { Id = Guid.NewGuid(), Name = "Kopierpapier A4", Basiseinheit = "Blatt", GrundpreisMenge = 100m });
        var artikel = await fabrik.SpeichernAsync(new Artikel
        {
            Id = Guid.NewGuid(),
            KorrespondentId = korrespondent.Id,
            Name = "Papier A4 500 Bl.",
            NameNormalisiert = "papier a4 500 bl",
            Einheit = "Pck",
            StammartikelId = stamm.Id,
            UmrechnungFaktor = 500m,
        });

        var geladen = await LadenAsync<Artikel>(a => a.Id == artikel.Id, q => q.Include(a => a.Stammartikel));

        geladen.Stammartikel!.GrundpreisMenge.ShouldBe(100m);
        geladen.UmrechnungFaktor.ShouldBe(500m);
    }

    [Fact]
    public async Task ImportVorgang_mit_Schritten_als_json()
    {
        var vorgang = await fabrik.SpeichernAsync(new ImportVorgang
        {
            Id = Guid.NewGuid(),
            Quelle = ImportQuelle.Mail,
            Status = ImportStatus.Wartet,
            Dateiname = "re.pdf",
            Groesse = 2048,
            MailMessageId = "<abc@example.org>",
            MailAbsender = "rechnung@example.org",
            MailBetreff = "Ihre Rechnung",
            Versuche = 2,
            NaechsterVersuchAm = Jetzt.AddMinutes(5),
            ErstelltAm = Jetzt,
            Schritte = [new ImportSchritt { Name = "Erkennen", Status = "Fertig" }, new ImportSchritt { Name = "Extrahieren", Status = "Wartet" }],
        });

        var geladen = await LadenAsync<ImportVorgang>(v => v.Id == vorgang.Id);

        geladen.Status.ShouldBe(ImportStatus.Wartet);
        geladen.Schritte.Select(s => $"{s.Name}:{s.Status}").ShouldBe(["Erkennen:Fertig", "Extrahieren:Wartet"]);
    }

    [Fact]
    public async Task Benutzer_mit_Token_Ansicht_und_gelesener_Benachrichtigung()
    {
        var benutzer = await fabrik.SpeichernAsync(Testdaten.Benutzer(anzeigename: "Anna"));
        var token = await fabrik.SpeichernAsync(new ApiToken
        {
            Id = Guid.NewGuid(),
            BenutzerId = benutzer.Id,
            Name = "Claude",
            Hash = Guid.NewGuid().ToString("N"),
            Endung = "x7Qa",
            Scopes = ["lesen", "upload"],
            ErstelltAm = Jetzt,
        });
        var ansicht = await fabrik.SpeichernAsync(new GespeicherteAnsicht
        {
            Id = Guid.NewGuid(),
            BenutzerId = benutzer.Id,
            Name = "Offen",
            Definition = """{"status":"Offen"}""",
        });
        var benachrichtigung = new Benachrichtigung
        {
            Id = Guid.NewGuid(),
            Art = BenachrichtigungArt.ImportFehler,
            Schweregrad = Schweregrad.Fehler,
            Titel = "Import fehlgeschlagen",
            Text = "re.pdf",
            NurAdmins = true,
            EntdoppelungsSchluessel = $"import:{Guid.NewGuid()}",
            ErstelltAm = Jetzt,
        };
        benachrichtigung.Gelesen.Add(new BenachrichtigungGelesen { BenutzerId = benutzer.Id, GelesenAm = Jetzt });
        await fabrik.SpeichernAsync(benachrichtigung);

        (await LadenAsync<Benutzer>(b => b.Id == benutzer.Id)).Anzeigename.ShouldBe("Anna");
        (await LadenAsync<ApiToken>(t => t.Id == token.Id)).Scopes.ShouldBe(["lesen", "upload"]);
        (await LadenAsync<GespeicherteAnsicht>(a => a.Id == ansicht.Id)).Definition.ShouldContain("\"status\"");
        var geladen = await LadenAsync<Benachrichtigung>(b => b.Id == benachrichtigung.Id, q => q.Include(b => b.Gelesen));
        geladen.Schweregrad.ShouldBe(Schweregrad.Fehler);
        geladen.Gelesen.ShouldHaveSingleItem().BenutzerId.ShouldBe(benutzer.Id);
    }

    [Fact]
    public async Task Betriebsdaten_werden_gespeichert_und_geladen()
    {
        var schluessel = $"test.{Guid.NewGuid():N}";
        var monat = new DateOnly(2000 + Random.Shared.Next(1, 999), 1, 1);
        await fabrik.SpeichernAsync(new Einstellung { Schluessel = schluessel, Wert = """{"limit":500}""", GeaendertAm = Jetzt });
        await fabrik.SpeichernAsync(new DiVerbrauch { Monat = monat, Seiten = 42 });
        var modell = await fabrik.SpeichernAsync(new MatchingModell
        {
            Id = Guid.NewGuid(),
            Ziel = "Tag",
            Daten = [1, 2, 3],
            TrainiertAm = Jetzt,
            AnzahlBeispiele = 12,
            Guete = 0.87,
        });
        var export = await fabrik.SpeichernAsync(new ExportLauf
        {
            Id = Guid.NewGuid(),
            ErstelltAm = Jetzt,
            BenutzerId = Guid.NewGuid(),
            BenutzerName = "Anna",
            Von = new DateOnly(2025, 1, 1),
            Bis = new DateOnly(2025, 12, 31),
            Bezeichnung = "Jahr 2025",
            Inhalt = "Belegliste (xlsx), Summen, Vorsteuer, ZIP",
            AnzahlBelege = 3,
            Dateipfad = "exporte/2025.zip",
            Dateiname = "2025.zip",
        });
        var lauf = await fabrik.SpeichernAsync(new IntegritaetsLauf
        {
            Id = Guid.NewGuid(),
            GestartetAm = Jetzt,
            BeendetAm = Jetzt.AddMinutes(1),
            AnzahlDateien = 10,
            AnzahlAbweichungen = 1,
            Abweichungen = ["2026/01/re.pdf"],
        });

        (await LadenAsync<Einstellung>(e => e.Schluessel == schluessel)).Wert.ShouldContain("500");
        (await LadenAsync<DiVerbrauch>(d => d.Monat == monat)).Seiten.ShouldBe(42);
        (await LadenAsync<MatchingModell>(m => m.Id == modell.Id)).Daten.ShouldBe(new byte[] { 1, 2, 3 });
        (await LadenAsync<ExportLauf>(e => e.Id == export.Id)).Bis.ShouldBe(new DateOnly(2025, 12, 31));
        (await LadenAsync<IntegritaetsLauf>(l => l.Id == lauf.Id)).Abweichungen.ShouldBe(["2026/01/re.pdf"]);
    }

    [Fact]
    public async Task Seed_Daten_sind_vorhanden()
    {
        await using var datenbank = await FrischeDatenbank.AnlegenAsync(fabrik);
        await using var db = datenbank.Kontext();
        await db.Database.MigrateAsync();

        (await db.Roles.OrderBy(r => r.Name).Select(r => r.Name).ToListAsync()).ShouldBe(["Admin", "Benutzer", "NurLesen"]);
        (await db.Roles.SingleAsync(r => r.Id == SystemIds.RolleNurLesen)).NormalizedName.ShouldBe("NURLESEN");

        var zuPruefen = await db.Tags.SingleAsync(t => t.Id == SystemIds.TagZuPruefen);
        zuPruefen.Name.ShouldBe("zu prüfen");
        zuPruefen.Farbe.ShouldBe("orange");
        zuPruefen.IstSystem.ShouldBeTrue();
        zuPruefen.Matching.Algorithmus.ShouldBe(MatchingAlgorithmus.Keiner);

        var inbox = await db.Tags.SingleAsync(t => t.Id == SystemIds.TagInbox);
        inbox.Name.ShouldBe("Inbox");
        inbox.Farbe.ShouldBe("blau");
        inbox.IstInbox.ShouldBeTrue();
    }

    [Fact]
    public async Task Kategoriename_ist_eindeutig()
    {
        var kategorie = await fabrik.SpeichernAsync(Testdaten.Kategorie());

        await VerletztEindeutigkeitAsync(Testdaten.Kategorie(kategorie.Name));
    }

    [Fact]
    public async Task Tagname_ist_eindeutig()
    {
        var tag = await fabrik.SpeichernAsync(Testdaten.Tag());

        await VerletztEindeutigkeitAsync(Testdaten.Tag(tag.Name));
    }

    [Fact]
    public async Task Artikel_ist_je_Korrespondent_und_normalisiertem_Namen_eindeutig()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var anderer = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        await fabrik.SpeichernAsync(new Artikel { Id = Guid.NewGuid(), KorrespondentId = korrespondent.Id, Name = "Toner", NameNormalisiert = "toner" });
        await fabrik.SpeichernAsync(new Artikel { Id = Guid.NewGuid(), KorrespondentId = anderer.Id, Name = "Toner", NameNormalisiert = "toner" });

        await VerletztEindeutigkeitAsync(new Artikel { Id = Guid.NewGuid(), KorrespondentId = korrespondent.Id, Name = "TONER", NameNormalisiert = "toner" });
    }

    [Fact]
    public async Task Artikel_mit_Nummer_ist_je_Korrespondent_ueber_die_Nummer_eindeutig()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        Artikel Neu(string nummer, string name = "Pressnippel") =>
            new() { Id = Guid.NewGuid(), KorrespondentId = korrespondent.Id, Artikelnummer = nummer, Name = name, NameNormalisiert = name.ToLowerInvariant() };

        // Gleicher Name mit verschiedenen Nummern und daneben ein gleichnamiger Artikel ohne Nummer sind erlaubt.
        await fabrik.SpeichernAsync(Neu("5G8FDLORX"));
        await fabrik.SpeichernAsync(Neu("4G12FDLORX"));
        await fabrik.SpeichernAsync(new Artikel { Id = Guid.NewGuid(), KorrespondentId = korrespondent.Id, Name = "Pressnippel", NameNormalisiert = "pressnippel" });

        await VerletztEindeutigkeitAsync(Neu("5G8FDLORX", "Anderer Name"));
    }

    [Fact]
    public async Task ApiToken_Hash_ist_eindeutig()
    {
        var benutzer = await fabrik.SpeichernAsync(Testdaten.Benutzer());
        var hash = Guid.NewGuid().ToString("N");
        await fabrik.SpeichernAsync(new ApiToken { Id = Guid.NewGuid(), BenutzerId = benutzer.Id, Name = "a", Hash = hash, Endung = "abcd", ErstelltAm = Jetzt });

        await VerletztEindeutigkeitAsync(new ApiToken { Id = Guid.NewGuid(), BenutzerId = benutzer.Id, Name = "b", Hash = hash, Endung = "abcd", ErstelltAm = Jetzt });
    }

    [Fact]
    public async Task Entdoppelungsschluessel_ist_eindeutig_wenn_gesetzt()
    {
        var schluessel = $"di-limit:{Guid.NewGuid()}";
        await fabrik.SpeichernAsync(Benachrichtigung(schluessel));
        await fabrik.SpeichernAsync(Benachrichtigung(null));
        await fabrik.SpeichernAsync(Benachrichtigung(null));

        await VerletztEindeutigkeitAsync(Benachrichtigung(schluessel));
    }

    [Fact]
    public async Task Korrespondent_mit_Artikeln_laesst_sich_nicht_loeschen()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        await fabrik.SpeichernAsync(new Artikel { Id = Guid.NewGuid(), KorrespondentId = korrespondent.Id, Name = "x", NameNormalisiert = "x" });

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var fehler = await Should.ThrowAsync<PostgresException>(() =>
            db.Korrespondenten.Where(k => k.Id == korrespondent.Id).ExecuteDeleteAsync());
        fehler.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
    }

    private static Benachrichtigung Benachrichtigung(string? schluessel) => new()
    {
        Id = Guid.NewGuid(),
        Art = BenachrichtigungArt.DiLimitWarnung,
        Schweregrad = Schweregrad.Warnung,
        Titel = "Limit",
        Text = "80 %",
        EntdoppelungsSchluessel = schluessel,
        ErstelltAm = Jetzt,
    };

    private async Task<T> LadenAsync<T>(
        System.Linq.Expressions.Expression<Func<T, bool>> filter,
        Func<IQueryable<T>, IQueryable<T>>? erweitern = null)
        where T : class
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        IQueryable<T> abfrage = db.Set<T>().AsNoTracking();
        return await (erweitern?.Invoke(abfrage) ?? abfrage).SingleAsync(filter);
    }

    private async Task VerletztEindeutigkeitAsync<T>(T entitaet)
        where T : class
    {
        var fehler = await Should.ThrowAsync<DbUpdateException>(() => fabrik.SpeichernAsync(entitaet));
        fehler.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }
}
