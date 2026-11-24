using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

/// <summary>Ohne Fremdschlüssel auf den Benutzer: Der Export bleibt nachvollziehbar, auch wenn das Konto gelöscht wird.</summary>
public sealed class ExportLaufKonfiguration : IEntityTypeConfiguration<ExportLauf>
{
    public void Configure(EntityTypeBuilder<ExportLauf> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BenutzerName).HasMaxLength(200);
        builder.Property(e => e.Bezeichnung).HasMaxLength(300);
        builder.Property(e => e.Inhalt).HasMaxLength(500);
        builder.Property(e => e.Dateipfad).HasMaxLength(1000);
        builder.Property(e => e.Dateiname).HasMaxLength(300);
        builder.HasIndex(e => e.ErstelltAm);
    }
}
