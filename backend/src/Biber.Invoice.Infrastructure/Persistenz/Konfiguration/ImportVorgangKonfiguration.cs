using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class ImportVorgangKonfiguration : IEntityTypeConfiguration<ImportVorgang>
{
    public void Configure(EntityTypeBuilder<ImportVorgang> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Quelle).HasMaxLength(20);
        builder.Property(v => v.Status).HasMaxLength(20);
        builder.Property(v => v.Dateiname).HasMaxLength(500);
        builder.Property(v => v.Sha256).HasMaxLength(64);
        builder.Property(v => v.StagingPfad).HasMaxLength(1000);
        builder.Property(v => v.HerkunftPfad).HasMaxLength(1000);
        builder.Property(v => v.MailMessageId).HasMaxLength(1000);
        builder.Property(v => v.MailAbsender).HasMaxLength(500);
        builder.Property(v => v.MailBetreff).HasMaxLength(1000);

        builder.OwnsMany(v => v.Schritte, s => s.ToJson("schritte"));
        builder.HasIndex(v => v.Status);
        builder.HasIndex(v => v.Sha256);
        builder.HasIndex(v => v.ErstelltAm);

        builder.HasOne<Beleg>().WithMany().HasForeignKey(v => v.BelegId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Benutzer>().WithMany().HasForeignKey(v => v.BenutzerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ApiToken>().WithMany().HasForeignKey(v => v.ApiTokenId).OnDelete(DeleteBehavior.SetNull);
    }
}
