using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class BelegKonfiguration : IEntityTypeConfiguration<Beleg>
{
    /// <summary>Name der generierten Volltext-Spalte (Schatteneigenschaft, <c>tsvector</c>).</summary>
    public const string Suchvektor = "Suchvektor";

    public void Configure(EntityTypeBuilder<Beleg> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Richtung).HasMaxLength(20);
        builder.Property(b => b.Belegart).HasMaxLength(20);
        builder.Property(b => b.Status).HasMaxLength(20);
        builder.Property(b => b.Quelle).HasMaxLength(20);
        builder.Property(b => b.KorrespondentErkennung).HasMaxLength(20);
        builder.Property(b => b.ExtraktionsQuelle).HasMaxLength(30);
        builder.Property(b => b.Titel).HasMaxLength(500);
        builder.Property(b => b.Rechnungsnummer).HasMaxLength(200);
        builder.Property(b => b.Waehrung).HasMaxLength(3).HasDefaultValue("EUR");
        builder.Property(b => b.DiKonfidenz).HasPrecision(5, 4);

        builder.Property<NpgsqlTsVector>(Suchvektor)
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('german', coalesce(titel, '') || ' ' || coalesce(rechnungsnummer, '') || ' ' || coalesce(volltext, ''))",
                stored: true);
        builder.HasIndex(Suchvektor).HasMethod("gin");

        builder.HasIndex(b => b.Rechnungsdatum);
        builder.HasIndex(b => b.Faelligkeitsdatum);
        builder.HasIndex(b => b.KorrespondentId);
        builder.HasIndex(b => b.KategorieId);
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.Rechnungsnummer)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("IX_Beleg_Rechnungsnummer_Trgm");

        builder.HasOne(b => b.Korrespondent).WithMany().HasForeignKey(b => b.KorrespondentId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(b => b.Kategorie).WithMany().HasForeignKey(b => b.KategorieId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ImportVorgang>().WithMany().HasForeignKey(b => b.ImportVorgangId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Beleg>().WithMany().HasForeignKey(b => b.DuplikatVonId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Beleg>().WithMany().HasForeignKey(b => b.AufgeteiltAusBelegId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Benutzer>().WithMany().HasForeignKey(b => b.BestaetigtVonId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Benutzer>().WithMany().HasForeignKey(b => b.ErstelltVonId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(b => b.Dateien).WithOne().HasForeignKey(d => d.BelegId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Steuerzeilen).WithOne().HasForeignKey(s => s.BelegId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Positionen).WithOne().HasForeignKey(p => p.BelegId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Tags).WithOne().HasForeignKey(t => t.BelegId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Befunde).WithOne().HasForeignKey(f => f.BelegId).OnDelete(DeleteBehavior.Cascade);
    }
}
