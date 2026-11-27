using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public static class JsonbListe
{
    /// <summary>
    /// Speichert eine Liste von Records (Bankverbindungen, Merkmale) als <c>jsonb</c>; Änderungen erkennt EF am Inhalt.
    /// </summary>
    public static PropertyBuilder<List<T>> AlsJsonb<T>(this PropertyBuilder<List<T>> eigenschaft)
        where T : notnull =>
        eigenschaft
            .HasColumnType("jsonb")
            .HasConversion(
                liste => JsonSerializer.Serialize(liste, JsonSerializerOptions.Default),
                json => JsonSerializer.Deserialize<List<T>>(json, JsonSerializerOptions.Default) ?? new List<T>(),
                new ValueComparer<List<T>>(
                    (a, b) => a!.SequenceEqual(b!),
                    liste => liste.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
                    liste => liste.ToList()));
}
