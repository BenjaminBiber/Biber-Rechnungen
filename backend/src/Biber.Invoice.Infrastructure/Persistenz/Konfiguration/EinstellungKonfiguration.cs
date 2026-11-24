using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class EinstellungKonfiguration : IEntityTypeConfiguration<Einstellung>
{
    public void Configure(EntityTypeBuilder<Einstellung> builder)
    {
        builder.HasKey(e => e.Schluessel);
        builder.Property(e => e.Schluessel).HasMaxLength(200);
        builder.Property(e => e.Wert).HasColumnType("jsonb");
    }
}
