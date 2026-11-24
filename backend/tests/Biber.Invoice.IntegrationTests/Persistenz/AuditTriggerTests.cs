using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Biber.Invoice.IntegrationTests.Persistenz;

[Collection("Api")]
public sealed class AuditTriggerTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Insert_gelingt()
    {
        var eintrag = await fabrik.SpeichernAsync(NeuerEintrag());

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var geladen = await db.AuditEintraege.SingleAsync(a => a.Id == eintrag.Id);
        eintrag.Id.ShouldBeGreaterThan(0);
        geladen.BenutzerName.ShouldBe("System");
        geladen.NeuerWert.ShouldBe("Fertig");
    }

    [Fact]
    public async Task Update_schlaegt_fehl()
    {
        var eintrag = await fabrik.SpeichernAsync(NeuerEintrag());
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();

        var fehler = await Should.ThrowAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE audit_eintrag SET neuer_wert = 'Fehler' WHERE id = {eintrag.Id}"));

        fehler.MessageText.ShouldContain("UPDATE");
        (await db.AuditEintraege.SingleAsync(a => a.Id == eintrag.Id)).NeuerWert.ShouldBe("Fertig");
    }

    [Fact]
    public async Task Update_ueber_EF_schlaegt_fehl()
    {
        var eintrag = await fabrik.SpeichernAsync(NeuerEintrag());
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var geladen = await db.AuditEintraege.SingleAsync(a => a.Id == eintrag.Id);

        geladen.NeuerWert = "Fehler";

        var fehler = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        fehler.InnerException.ShouldBeOfType<PostgresException>();
    }

    [Fact]
    public async Task Delete_schlaegt_fehl()
    {
        var eintrag = await fabrik.SpeichernAsync(NeuerEintrag());
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();

        var fehler = await Should.ThrowAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM audit_eintrag WHERE id = {eintrag.Id}"));

        fehler.MessageText.ShouldContain("DELETE");
        (await db.AuditEintraege.AnyAsync(a => a.Id == eintrag.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task Zuruecksetzen_der_Testdatenbank_leert_auch_das_Protokoll()
    {
        await using var eigene = fabrik.MitDiensten(_ => { });
        await eigene.SpeichernAsync(NeuerEintrag());

        await eigene.ZuruecksetzenAsync(); // TRUNCATE löst den Zeilen-Trigger nicht aus

        using var scope = eigene.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.AuditEintraege.AnyAsync()).ShouldBeFalse();
    }

    private static AuditEintrag NeuerEintrag() => new()
    {
        Zeitpunkt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero),
        Entitaet = nameof(Beleg),
        EntitaetId = Guid.NewGuid().ToString(),
        Aktion = "Geaendert",
        Feld = nameof(Beleg.Status),
        AlterWert = "InVerarbeitung",
        NeuerWert = "Fertig",
    };
}
