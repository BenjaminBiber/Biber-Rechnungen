using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class MatchingModellKonfiguration : IEntityTypeConfiguration<MatchingModell>
{
    public void Configure(EntityTypeBuilder<MatchingModell> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Ziel).HasMaxLength(20);
        builder.Property(m => m.Daten).HasColumnType("bytea");
        builder.HasIndex(m => new { m.Ziel, m.TrainiertAm });
    }
}
