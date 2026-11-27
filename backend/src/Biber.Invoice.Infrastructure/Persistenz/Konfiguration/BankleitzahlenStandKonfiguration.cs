using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

public sealed class BankleitzahlenStandKonfiguration : IEntityTypeConfiguration<BankleitzahlenStand>
{
    public const int MaxFehlerLaenge = 1000;

    public void Configure(EntityTypeBuilder<BankleitzahlenStand> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Datei).HasMaxLength(500);
        builder.Property(s => s.LetzterFehler).HasMaxLength(MaxFehlerLaenge);
    }
}
