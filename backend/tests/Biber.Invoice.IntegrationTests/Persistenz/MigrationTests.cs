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
}
