using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Persistenz;

/// <summary>
/// Datenbankkontext der Anwendung. Entitätskonfigurationen werden per
/// <see cref="IEntityTypeConfiguration{TEntity}"/> aus dieser Assembly geladen.
/// </summary>
public class BiberDbContext(DbContextOptions<BiberDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BiberDbContext).Assembly);
    }
}
