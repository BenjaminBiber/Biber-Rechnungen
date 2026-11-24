using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class PositionKonfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Einheit).HasMaxLength(50);
        builder.Property(p => p.GrundpreisEinheit).HasMaxLength(50);

        // Mengen und Stückpreise brauchen mehr als zwei Nachkommastellen (z. B. 0,0125 € je Blatt).
        builder.Property(p => p.Menge).HasPrecision(14, 4);
        builder.Property(p => p.Einzelpreis).HasPrecision(14, 4);
        builder.Property(p => p.Grundpreis).HasPrecision(14, 4);
        builder.Property(p => p.Steuersatz).HasPrecision(5, 2);

        builder.HasIndex(p => new { p.BelegId, p.Nr });
        builder.HasIndex(p => p.ArtikelId);
        builder.HasOne(p => p.Artikel).WithMany().HasForeignKey(p => p.ArtikelId).OnDelete(DeleteBehavior.SetNull);
    }
}
