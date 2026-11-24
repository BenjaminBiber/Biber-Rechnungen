using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class StammartikelKonfiguration : IEntityTypeConfiguration<Stammartikel>
{
    public void Configure(EntityTypeBuilder<Stammartikel> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(500);
        builder.Property(s => s.Basiseinheit).HasMaxLength(50);
        builder.Property(s => s.GrundpreisMenge).HasPrecision(14, 4);
    }
}
