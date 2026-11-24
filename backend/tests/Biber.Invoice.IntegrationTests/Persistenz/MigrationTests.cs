using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
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
        angewendet.Count.ShouldBe(1);
        angewendet[0].ShouldEndWith("_Initial");
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await db.Tags.AnyAsync(t => t.Id == SystemIds.TagInbox)).ShouldBeTrue();
    }

    [Fact]
    public void Es_gibt_genau_eine_Migration_Initial()
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();

        db.Database.GetMigrations().ShouldHaveSingleItem().ShouldEndWith("_Initial");
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
