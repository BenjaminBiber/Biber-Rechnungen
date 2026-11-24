using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests;

[Collection("Api")]
public sealed class ApiFabrikTests(ApiFabrik fabrik)
{
    [Fact]
    public void Zeit_ist_die_Uhr_der_Anwendung()
    {
        var jetzt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        fabrik.Zeit.Setze(jetzt);

        fabrik.Services.GetRequiredService<TimeProvider>().GetUtcNow().ShouldBe(jetzt);
    }

    [Fact]
    public void Pfade_zeigen_in_den_Temp_Ordner()
    {
        var konfiguration = fabrik.Services.GetRequiredService<IConfiguration>();

        konfiguration["Pfade:Eingang"].ShouldBe(Path.Combine(fabrik.TempOrdner, "eingang"));
        konfiguration["Pfade:Ablage"].ShouldBe(Path.Combine(fabrik.TempOrdner, "ablage"));
    }

    [Fact]
    public async Task Zuruecksetzen_ohne_Tabellen_laeuft_durch()
    {
        await using var eigene = fabrik.MitDiensten(_ => { });

        await eigene.ZuruecksetzenAsync();
    }

    [Fact]
    public async Task Zuruecksetzen_leert_Tabellen_und_behaelt_Migrationshistorie()
    {
        // Eigene Fabrik, damit ihr Respawner die Testtabelle kennt und keine andere Fabrik sie sieht.
        await using var eigene = fabrik.MitDiensten(_ => { });
        using var scope = eigene.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" ("MigrationId" varchar(150) PRIMARY KEY, "ProductVersion" varchar(32) NOT NULL);
            INSERT INTO "__EFMigrationsHistory" VALUES ('99999999999999_Test', 'test') ON CONFLICT DO NOTHING;
            CREATE TABLE zuruecksetzen_test (id int PRIMARY KEY);
            INSERT INTO zuruecksetzen_test VALUES (1), (2);
            """);

        try
        {
            await eigene.ZuruecksetzenAsync();

            (await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM zuruecksetzen_test").SingleAsync()).ShouldBe(0);
            (await db.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '99999999999999_Test'")
                .SingleAsync()).ShouldBe(1);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                DROP TABLE zuruecksetzen_test;
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '99999999999999_Test';
                """);
        }
    }

    [Fact]
    public async Task Zuruecksetzen_stellt_den_Seed_der_Migration_wieder_her()
    {
        await fabrik.ZuruecksetzenAsync();
        using (var vorher = fabrik.ErzeugeScope())
        {
            var db = vorher.ServiceProvider.GetRequiredService<BiberDbContext>();
            db.Korrespondenten.Add(new Korrespondent { Name = "Seed-Test GmbH" });
            await db.SaveChangesAsync();
        }

        await fabrik.ZuruecksetzenAsync();

        using var scope = fabrik.ErzeugeScope();
        var dbNachher = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await dbNachher.Roles.Select(r => r.Name).OrderBy(n => n).ToListAsync())
            .ShouldBe(["Admin", "Benutzer", "NurLesen"]);
        var inbox = await dbNachher.Tags.SingleAsync(t => t.Id == SystemIds.TagInbox);
        inbox.Name.ShouldBe("Inbox");
        inbox.IstInbox.ShouldBeTrue();
        var zuPruefen = await dbNachher.Tags.SingleAsync(t => t.Id == SystemIds.TagZuPruefen);
        zuPruefen.Name.ShouldBe("zu prüfen");
        zuPruefen.IstSystem.ShouldBeTrue();
        zuPruefen.Matching.Algorithmus.ShouldBe(MatchingAlgorithmus.Keiner);
        (await dbNachher.Tags.CountAsync()).ShouldBe(2);
        (await dbNachher.Korrespondenten.AnyAsync()).ShouldBeFalse();
        (await dbNachher.Belege.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public void Ohne_HttpContext_ist_der_aktuelle_Benutzer_das_System()
    {
        using var scope = fabrik.ErzeugeScope();

        var benutzer = scope.ServiceProvider.GetRequiredService<IAktuellerBenutzer>();

        benutzer.IstSystem.ShouldBeTrue();
        benutzer.Anzeigename.ShouldBe("System");
        benutzer.Id.ShouldBeNull();
    }

    [Fact]
    public async Task Aktueller_Benutzer_kommt_aus_den_Claims()
    {
        await using var mitEndpunkt = fabrik.MitDiensten(d => d.AddSingleton<IEndpunktModul, BenutzerEndpunkt>());
        var client = mitEndpunkt.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.GetJsonAsync<BenutzerAntwort>("/api/test/ich");

        antwort.ShouldNotBeNull();
        antwort.Id.ShouldBe(ApiFabrik.BenutzerId(Rolle.Benutzer));
        antwort.IstSystem.ShouldBeFalse();
        antwort.Benutzer.ShouldBeTrue();
        antwort.Admin.ShouldBeFalse();
    }

    private sealed record BenutzerAntwort(Guid? Id, string Anzeigename, bool IstSystem, bool Benutzer, bool Admin);

    private sealed class BenutzerEndpunkt : IEndpunktModul
    {
        public void Zuordnen(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/test/ich", (IAktuellerBenutzer b) => TypedResults.Ok(new BenutzerAntwort(
                    b.Id, b.Anzeigename, b.IstSystem, b.HatRolle(Rolle.Benutzer), b.HatRolle(Rolle.Admin))))
                .RequireAuthorization(Richtlinien.Lesen);
        }
    }
}
