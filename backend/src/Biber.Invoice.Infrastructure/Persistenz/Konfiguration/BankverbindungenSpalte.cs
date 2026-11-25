using System.Text.Json;
using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public static class BankverbindungenSpalte
{
    /// <summary>Speichert die Liste als <c>jsonb</c>; Änderungen erkennt EF am Inhalt.</summary>
    public static PropertyBuilder<List<Bankverbindung>> AlsJsonb(this PropertyBuilder<List<Bankverbindung>> eigenschaft) =>
        eigenschaft
            .HasColumnType("jsonb")
            .HasConversion(
                liste => JsonSerializer.Serialize(liste, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<List<Bankverbindung>>(json, JsonSerializerOptions.Default) ?? new List<Bankverbindung>(),
                new ValueComparer<List<Bankverbindung>>(
                    (a, b) => a!.SequenceEqual(b!),
                    liste => liste.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
                    liste => liste.ToList()));
}
