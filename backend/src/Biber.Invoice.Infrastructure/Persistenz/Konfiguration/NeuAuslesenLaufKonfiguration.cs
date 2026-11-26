using System.Text.Json;
using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class NeuAuslesenLaufKonfiguration : IEntityTypeConfiguration<NeuAuslesenLauf>
{
    public const int MaxGrundLaenge = 1000;

    public void Configure(EntityTypeBuilder<NeuAuslesenLauf> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Abbruchgrund).HasMaxLength(MaxGrundLaenge);
        builder.Property(l => l.LetzterFehler).HasMaxLength(MaxGrundLaenge);
        builder.Property(l => l.FehlerBelege)
            .HasColumnType("jsonb")
            .HasConversion(
                liste => JsonSerializer.Serialize(liste, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<List<Guid>>(json, JsonSerializerOptions.Default) ?? new List<Guid>(),
                new ValueComparer<List<Guid>>(
                    (a, b) => a!.SequenceEqual(b!),
                    liste => liste.Aggregate(0, (h, g) => HashCode.Combine(h, g.GetHashCode())),
                    liste => liste.ToList()));
        builder.HasIndex(l => l.GestartetAm);
    }
}
