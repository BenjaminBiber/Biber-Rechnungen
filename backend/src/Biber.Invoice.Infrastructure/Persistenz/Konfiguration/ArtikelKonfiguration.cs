using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Regeln;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class ArtikelKonfiguration : IEntityTypeConfiguration<Artikel>
{
    public void Configure(EntityTypeBuilder<Artikel> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Name).HasMaxLength(500);
        builder.Property(a => a.NameNormalisiert).HasMaxLength(500);
        builder.Property(a => a.Artikelnummer).HasMaxLength(ArtikelnummerNormalisierer.MaxLaenge);
        builder.Property(a => a.Einheit).HasMaxLength(50);
        builder.Property(a => a.UmrechnungFaktor).HasPrecision(14, 4);

        // Eindeutig je Korrespondent: mit Nummer über die Nummer, sonst über den Namen (gleichnamige Artikel mit
        // verschiedenen Nummern sind verschiedene Artikel).
        builder.HasIndex(a => new { a.KorrespondentId, a.Artikelnummer }).IsUnique().HasFilter("artikelnummer IS NOT NULL");
        builder.HasIndex(a => new { a.KorrespondentId, a.NameNormalisiert }).IsUnique().HasFilter("artikelnummer IS NULL");
        builder.HasIndex(a => a.NameNormalisiert)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_Artikel_NameNormalisiert_Trgm");
        builder.HasIndex(a => a.StammartikelId);

        builder.HasOne(a => a.Korrespondent).WithMany().HasForeignKey(a => a.KorrespondentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Stammartikel).WithMany().HasForeignKey(a => a.StammartikelId).OnDelete(DeleteBehavior.SetNull);
    }
}
