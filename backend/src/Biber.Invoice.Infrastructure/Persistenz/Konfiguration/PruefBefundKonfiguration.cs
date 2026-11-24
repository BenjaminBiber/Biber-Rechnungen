using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class PruefBefundKonfiguration : IEntityTypeConfiguration<PruefBefund>
{
    public void Configure(EntityTypeBuilder<PruefBefund> builder)
    {
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Pruefung).HasMaxLength(30);
        builder.Property(f => f.Ergebnis).HasMaxLength(20);
        builder.HasIndex(f => f.BelegId);
    }
}
