using System.Text.Json;
using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class IntegritaetsLaufKonfiguration : IEntityTypeConfiguration<IntegritaetsLauf>
{
    public void Configure(EntityTypeBuilder<IntegritaetsLauf> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Abweichungen)
            .HasColumnType("jsonb")
            .HasConversion(
                liste => JsonSerializer.Serialize(liste, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<List<string>>(json, JsonSerializerOptions.Default) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (a, b) => a!.SequenceEqual(b!),
                    liste => liste.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                    liste => liste.ToList()));
        builder.HasIndex(l => l.GestartetAm);
    }
}
