using Biber.Invoice.Domain.Entitaeten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Persistenz;

/// <summary>
/// Datenbankkontext der Anwendung. Entitätskonfigurationen werden per
/// <see cref="IEntityTypeConfiguration{TEntity}"/> aus dieser Assembly geladen.
/// Enums werden als Text gespeichert, Geld als <c>numeric(14,2)</c>, alle Namen in snake_case.
/// </summary>
public class BiberDbContext(DbContextOptions<BiberDbContext> options)
    : IdentityDbContext<Benutzer, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Beleg> Belege => Set<Beleg>();
    public DbSet<BelegDatei> BelegDateien => Set<BelegDatei>();
    public DbSet<Steuerzeile> Steuerzeilen => Set<Steuerzeile>();
    public DbSet<Position> Positionen => Set<Position>();
    public DbSet<BelegTag> BelegTags => Set<BelegTag>();
    public DbSet<PruefBefund> PruefBefunde => Set<PruefBefund>();
    public DbSet<Korrespondent> Korrespondenten => Set<Korrespondent>();
    public DbSet<Kategorie> Kategorien => Set<Kategorie>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Artikel> Artikel => Set<Artikel>();
    public DbSet<Stammartikel> Stammartikel => Set<Stammartikel>();
    public DbSet<ImportVorgang> ImportVorgaenge => Set<ImportVorgang>();
    public DbSet<AuditEintrag> AuditEintraege => Set<AuditEintrag>();
    public DbSet<Benachrichtigung> Benachrichtigungen => Set<Benachrichtigung>();
    public DbSet<BenachrichtigungGelesen> BenachrichtigungenGelesen => Set<BenachrichtigungGelesen>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<Einstellung> Einstellungen => Set<Einstellung>();
    public DbSet<DiVerbrauch> DiVerbraeuche => Set<DiVerbrauch>();
    public DbSet<MatchingModell> MatchingModelle => Set<MatchingModell>();
    public DbSet<ExportLauf> ExportLaeufe => Set<ExportLauf>();
    public DbSet<IntegritaetsLauf> IntegritaetsLaeufe => Set<IntegritaetsLauf>();
    public DbSet<NeuAuslesenLauf> NeuAuslesenLaeufe => Set<NeuAuslesenLauf>();
    public DbSet<GespeicherteAnsicht> GespeicherteAnsichten => Set<GespeicherteAnsicht>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
        configurationBuilder.Properties<decimal>().HavePrecision(14, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BiberDbContext).Assembly);
        SnakeCase.Anwenden(modelBuilder);
    }
}
