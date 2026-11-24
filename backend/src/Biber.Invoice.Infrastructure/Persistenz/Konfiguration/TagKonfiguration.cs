using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class TagKonfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(100);
        builder.Property(t => t.Farbe).HasMaxLength(20);
        builder.HasIndex(t => t.Name).IsUnique();

        builder.HasData(
            new { Id = SystemIds.TagZuPruefen, Name = "zu prüfen", Farbe = "orange", IstInbox = false, IstZahlungsTag = false, IstSystem = true },
            new { Id = SystemIds.TagInbox, Name = "Inbox", Farbe = "blau", IstInbox = true, IstZahlungsTag = false, IstSystem = false });
        builder.MitMatching().HasData(
            new { TagId = SystemIds.TagZuPruefen, Algorithmus = MatchingAlgorithmus.Keiner, GrossKlein = false },
            new { TagId = SystemIds.TagInbox, Algorithmus = MatchingAlgorithmus.Keiner, GrossKlein = false });
    }
}
