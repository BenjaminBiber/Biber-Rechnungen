using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class SteuerzeileKonfiguration : IEntityTypeConfiguration<Steuerzeile>
{
    public void Configure(EntityTypeBuilder<Steuerzeile> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Satz).HasPrecision(5, 2);
        builder.HasIndex(s => s.BelegId);
    }
}
