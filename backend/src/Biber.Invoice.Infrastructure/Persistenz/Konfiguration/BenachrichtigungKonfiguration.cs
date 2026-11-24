using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class BenachrichtigungKonfiguration : IEntityTypeConfiguration<Benachrichtigung>
{
    public void Configure(EntityTypeBuilder<Benachrichtigung> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Art).HasMaxLength(30);
        builder.Property(b => b.Schweregrad).HasMaxLength(20);
        builder.Property(b => b.Titel).HasMaxLength(300);
        builder.Property(b => b.Link).HasMaxLength(1000);
        builder.Property(b => b.EntdoppelungsSchluessel).HasMaxLength(300);

        // Postgres behandelt NULL-Werte in eindeutigen Indizes als verschieden: eindeutig nur, wenn gesetzt.
        builder.HasIndex(b => b.EntdoppelungsSchluessel).IsUnique();
        builder.HasIndex(b => b.ErstelltAm);

        builder.HasMany(b => b.Gelesen).WithOne().HasForeignKey(g => g.BenachrichtigungId).OnDelete(DeleteBehavior.Cascade);
    }
}
