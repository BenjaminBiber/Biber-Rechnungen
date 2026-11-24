using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class GespeicherteAnsichtKonfiguration : IEntityTypeConfiguration<GespeicherteAnsicht>
{
    public void Configure(EntityTypeBuilder<GespeicherteAnsicht> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(200);
        builder.Property(a => a.Definition).HasColumnType("jsonb");
        builder.HasIndex(a => a.BenutzerId);
        builder.HasOne<Benutzer>().WithMany().HasForeignKey(a => a.BenutzerId).OnDelete(DeleteBehavior.Cascade);
    }
}
