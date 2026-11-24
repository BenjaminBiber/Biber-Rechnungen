using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class BenachrichtigungGelesenKonfiguration : IEntityTypeConfiguration<BenachrichtigungGelesen>
{
    public void Configure(EntityTypeBuilder<BenachrichtigungGelesen> builder)
    {
        builder.HasKey(g => new { g.BenachrichtigungId, g.BenutzerId });
        builder.HasIndex(g => g.BenutzerId);
        builder.HasOne<Benutzer>().WithMany().HasForeignKey(g => g.BenutzerId).OnDelete(DeleteBehavior.Cascade);
    }
}
