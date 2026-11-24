using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class KorrespondentKonfiguration : IEntityTypeConfiguration<Korrespondent>
{
    public void Configure(EntityTypeBuilder<Korrespondent> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Name).HasMaxLength(300);
        builder.Property(k => k.Aliase).HasColumnType("text[]");
        builder.Property(k => k.UstIdNr).HasMaxLength(20);
        builder.Property(k => k.Steuernummer).HasMaxLength(30);
        builder.Property(k => k.Ibans).HasColumnType("text[]");
        builder.Property(k => k.Website).HasMaxLength(500);
        builder.Property(k => k.Domain).HasMaxLength(255);
        builder.Property(k => k.Email).HasMaxLength(320);
        builder.Property(k => k.LogoPfad).HasMaxLength(1000);
        builder.Property(k => k.LogoQuelle).HasMaxLength(20);

        builder.OwnsOne(k => k.Anschrift, a =>
        {
            a.Property(x => x.Strasse).HasMaxLength(300);
            a.Property(x => x.Plz).HasMaxLength(20);
            a.Property(x => x.Ort).HasMaxLength(200);
            a.Property(x => x.Land).HasMaxLength(100);
        });
        builder.Navigation(k => k.Anschrift).IsRequired();
        builder.MitMatching();

        builder.HasIndex(k => k.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_Korrespondent_Name_Trgm");
        builder.HasIndex(k => k.UstIdNr);

        builder.HasOne(k => k.DefaultKategorie).WithMany().HasForeignKey(k => k.DefaultKategorieId).OnDelete(DeleteBehavior.SetNull);
    }
}
