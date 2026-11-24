using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class ApiTokenKonfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.Property(t => t.Hash).HasMaxLength(200);
        builder.Property(t => t.Endung).HasMaxLength(4);
        builder.Property(t => t.Scopes).HasColumnType("text[]");

        builder.HasIndex(t => t.Hash).IsUnique();
        builder.HasIndex(t => t.BenutzerId);
        builder.HasOne<Benutzer>().WithMany().HasForeignKey(t => t.BenutzerId).OnDelete(DeleteBehavior.Cascade);
    }
}
