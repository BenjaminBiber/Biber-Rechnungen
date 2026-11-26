using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Persistenz;

[Collection("Api")]
public sealed class MigrationTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Migration_laeuft_auf_leerer_Datenbank()
    {
        await using var datenbank = await FrischeDatenbank.AnlegenAsync(fabrik);
        await using var db = datenbank.Kontext();

        await db.Database.MigrateAsync();

        var angewendet = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        angewendet[0].ShouldEndWith("_Initial");
        angewendet.ShouldContain(m => m.EndsWith("_Bankverbindungen"));
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await db.Tags.AnyAsync(t => t.Id == SystemIds.TagInbox)).ShouldBeTrue();
    }

    [Fact]
    public void Migrationen_bauen_auf_Initial_auf()
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();

        var migrationen = db.Database.GetMigrations().ToList();
        migrationen[0].ShouldEndWith("_Initial");
        migrationen.ShouldContain(m => m.EndsWith("_Bankverbindungen"));
    }

    [Fact]
    public async Task Bankverbindungen_uebernimmt_vorhandene_IBANs_in_ihrer_Reihenfolge()
    {
        await using var datenbank = await FrischeDatenbank.AnlegenAsync(fabrik);
        await using var db = datenbank.Kontext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("Initial");
        var mitIbans = Guid.NewGuid();
        var ohneIbans = Guid.NewGuid();
        const string Einfuegen =
            """
            INSERT INTO korrespondent (id, name, aliase, ibans, logo_quelle, ist_lieferant, ist_kunde,
                matching_algorithmus, matching_gross_klein, erstellt_am, geprueft)
            VALUES ({0}, {1}, '{{}}', {2}, 'Keine', true, false, 'Keiner', false, now(), true)
            """;
        await db.Database.ExecuteSqlRawAsync(Einfuegen, mitIbans, "Mit IBANs", new[] { "DE89370400440532013000", "DE02120300000000202051" });
        await db.Database.ExecuteSqlRawAsync(Einfuegen, ohneIbans, "Ohne IBANs", Array.Empty<string>());

        await migrator.MigrateAsync();

        (await db.Korrespondenten.AsNoTracking().SingleAsync(k => k.Id == mitIbans)).Bankverbindungen
            .ShouldBe([new Bankverbindung("DE89370400440532013000"), new Bankverbindung("DE02120300000000202051")]);
        (await db.Korrespondenten.AsNoTracking().SingleAsync(k => k.Id == ohneIbans)).Bankverbindungen.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerwaisteArtikelEntfernen_loescht_Artikel_ohne_Position_und_leere_Stammartikel()
    {
        await using var datenbank = await FrischeDatenbank.AnlegenAsync(fabrik);
        await using var db = datenbank.Kontext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("BelegBankverbindungen");
        // Per SQL: Das aktuelle Modell hat Spalten, die es in diesem Stand noch nicht gibt.
        var korrespondent = Testdaten.Korrespondent();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO korrespondent (id, name, aliase, bankverbindungen, logo_quelle, ist_lieferant, ist_kunde,
                matching_algorithmus, matching_gross_klein, erstellt_am, geprueft)
            VALUES ({korrespondent.Id}, {korrespondent.Name}, ARRAY[]::text[], '[]'::jsonb, 'Keine', true, false, 'Keiner', false, now(), true)
            """);
        var leer = new Stammartikel { Id = Guid.NewGuid(), Name = "Papier", Basiseinheit = "Blatt" };
        var gemischt = new Stammartikel { Id = Guid.NewGuid(), Name = "Toner", Basiseinheit = "Stk" };
        db.AddRange(leer, gemischt);
        await db.SaveChangesAsync();
        var gekauft = await ArtikelEinfuegenAsync(db, korrespondent.Id, "toner", gemischt.Id);
        await ArtikelEinfuegenAsync(db, korrespondent.Id, "papier", leer.Id);
        await ArtikelEinfuegenAsync(db, korrespondent.Id, "toner alt", gemischt.Id);
        var beleg = await BelegEinfuegenAsync(db, korrespondentId: korrespondent.Id);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO position (id, beleg_id, nr, text, artikel_id) VALUES ({Guid.NewGuid()}, {beleg}, 1, 'Toner', {gekauft})");

        await migrator.MigrateAsync();

        (await db.Artikel.AsNoTracking().Select(a => a.Id).ToListAsync()).ShouldBe([gekauft]);
        (await db.Stammartikel.AsNoTracking().Select(s => s.Id).ToListAsync()).ShouldBe([gemischt.Id]);
    }

    [Fact]
    public async Task SteuerzeilenAbleiten_ergaenzt_nur_eindeutige_Belege_ohne_Steuerzeile()
    {
        await using var datenbank = await FrischeDatenbank.AnlegenAsync(fabrik);
        await using var db = datenbank.Kontext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("VerwaisteArtikelEntfernen");
        Task<Guid> Mit(decimal? netto, decimal? steuer, decimal? brutto) => BelegEinfuegenAsync(db, netto, steuer, brutto);

        var neunzehn = await Mit(221.85m, 42.15m, 264.00m);
        var sieben = await Mit(100.00m, 7.00m, 107.00m);
        var ohneSteuer = await Mit(50.00m, 0m, 50.00m);
        await Mit(221.85m, 42.15m, 260.00m); // Summe falsch
        await Mit(100.00m, 12.00m, 112.00m); // kein üblicher Satz
        await Mit(null, null, 119.00m); // nur Brutto
        var schonAufgeteilt = await Mit(100.00m, 19.00m, 119.00m);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO steuerzeile (id, beleg_id, satz, netto, steuer) VALUES ({Guid.NewGuid()}, {schonAufgeteilt}, 19, 100.00, 19.00)");

        await migrator.MigrateAsync();

        var zeilen = await db.Set<Steuerzeile>().AsNoTracking().ToListAsync();
        zeilen.Count.ShouldBe(4);
        zeilen.Single(z => z.BelegId == neunzehn).ShouldSatisfyAllConditions(
            z => z.Satz.ShouldBe(19m), z => z.Netto.ShouldBe(221.85m), z => z.Steuer.ShouldBe(42.15m));
        zeilen.Single(z => z.BelegId == sieben).Satz.ShouldBe(7m);
        zeilen.Single(z => z.BelegId == ohneSteuer).Satz.ShouldBe(0m);
        zeilen.Single(z => z.BelegId == schonAufgeteilt).Satz.ShouldBe(19m);
    }

    [Fact]
    public void Modell_hat_keine_ausstehenden_Aenderungen()
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();

        db.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    [Fact]
    public async Task Migration_aktiviert_pg_trgm_und_legt_Audit_Trigger_an()
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();

        (await db.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname = 'pg_trgm'").SingleAsync()).ShouldBe(1);
        (await db.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM pg_trigger WHERE tgname = 'audit_eintrag_unveraenderlich'").SingleAsync()).ShouldBe(1);
    }

    /// <summary>
    /// Legt einen Beleg per SQL an. Ein Zwischenstand der Migrationen kennt nicht alle Spalten des aktuellen Modells
    /// (z. B. <c>geloescht_am</c>), deshalb nicht über den DbContext speichern.
    /// </summary>
    private static async Task<Guid> BelegEinfuegenAsync(
        BiberDbContext db, decimal? netto = null, decimal? steuer = null, decimal? brutto = null, Guid? korrespondentId = null)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO beleg (id, richtung, belegart, status, quelle, korrespondent_erkennung, kategorie_manuell,
                extraktions_quelle, seitenanzahl, erstellt_am, korrespondent_id, netto, steuer, brutto)
            VALUES ({id}, 'Eingang', 'Rechnung', 'Fertig', 'Upload', 'Keine', false, 'Manuell', 1, now(),
                {korrespondentId}, {netto}, {steuer}, {brutto})
            """);
        return id;
    }

    /// <summary>Per SQL: Das aktuelle Modell hat Spalten (z. B. <c>artikelnummer</c>), die es im alten Stand noch nicht gibt.</summary>
    private static async Task<Guid> ArtikelEinfuegenAsync(BiberDbContext db, Guid korrespondentId, string name, Guid? stammartikelId)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO artikel (id, korrespondent_id, name, name_normalisiert, stammartikel_id)
            VALUES ({id}, {korrespondentId}, {name}, {name}, {stammartikelId})
            """);
        return id;
    }
}
