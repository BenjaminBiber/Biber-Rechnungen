using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class BenutzerKonfiguration : IEntityTypeConfiguration<Benutzer>
{
    public void Configure(EntityTypeBuilder<Benutzer> builder)
    {
        builder.Property(b => b.Anzeigename).HasMaxLength(200);
    }
}
