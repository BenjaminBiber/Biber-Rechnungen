using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class KategorieKonfiguration : IEntityTypeConfiguration<Kategorie>
{
    public void Configure(EntityTypeBuilder<Kategorie> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Name).HasMaxLength(200);
        builder.Property(k => k.Farbe).HasMaxLength(9);
        builder.Property(k => k.EuerZeile).HasMaxLength(50);
        builder.MitMatching();
        builder.HasIndex(k => k.Name).IsUnique();
        builder.Ignore(k => k.VollerName);
        builder.Ignore(k => k.WirksameEuerZeile);
        builder.HasIndex(k => k.ElternId);
        builder.HasOne(k => k.Eltern).WithMany().HasForeignKey(k => k.ElternId).OnDelete(DeleteBehavior.Restrict);
    }
}
