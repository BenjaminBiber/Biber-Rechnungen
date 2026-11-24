using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

/// <summary>Bildet den Owned-Typ <see cref="Matching"/> in Spalten <c>matching_*</c> der besitzenden Tabelle ab.</summary>
internal static class MatchingKonfiguration
{
    public static OwnedNavigationBuilder<T, Matching> MitMatching<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        var matching = builder.OwnsOne<Matching>(nameof(Matching));
        matching.Property(m => m.Algorithmus).HasMaxLength(20);
        matching.Property(m => m.Muster).HasMaxLength(1000);
        builder.Navigation(nameof(Matching)).IsRequired();
        return matching;
    }
}
