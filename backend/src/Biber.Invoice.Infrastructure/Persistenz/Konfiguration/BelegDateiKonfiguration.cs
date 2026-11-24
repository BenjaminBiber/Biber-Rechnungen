using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class BelegDateiKonfiguration : IEntityTypeConfiguration<BelegDatei>
{
    public void Configure(EntityTypeBuilder<BelegDatei> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Rolle).HasMaxLength(20);
        builder.Property(d => d.Pfad).HasMaxLength(1000);
        builder.Property(d => d.Sha256).HasMaxLength(64);
        builder.Property(d => d.MimeType).HasMaxLength(100);
        builder.HasIndex(d => d.BelegId);
        builder.HasIndex(d => d.Sha256);
    }
}
