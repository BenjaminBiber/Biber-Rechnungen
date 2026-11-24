using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class BelegTagKonfiguration : IEntityTypeConfiguration<BelegTag>
{
    public void Configure(EntityTypeBuilder<BelegTag> builder)
    {
        builder.HasKey(t => new { t.BelegId, t.TagId });
        builder.HasIndex(t => t.TagId);
        builder.HasOne(t => t.Tag).WithMany().HasForeignKey(t => t.TagId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Benutzer>().WithMany().HasForeignKey(t => t.GesetztVonId).OnDelete(DeleteBehavior.SetNull);
    }
}
