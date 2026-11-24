using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class DiVerbrauchKonfiguration : IEntityTypeConfiguration<DiVerbrauch>
{
    public void Configure(EntityTypeBuilder<DiVerbrauch> builder)
    {
        builder.HasKey(d => d.Monat);
        builder.Property(d => d.Monat).ValueGeneratedNever();
    }
}
