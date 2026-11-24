using Biber.Invoice.Domain.Entitaeten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

/// <summary>
/// Append-only: Ein Trigger aus der Initial-Migration lehnt <c>UPDATE</c> und <c>DELETE</c> ab.
/// Deshalb keine Fremdschlüssel – sie würden beim Löschen eines Benutzers oder Belegs ändern wollen.
/// </summary>
public sealed class AuditEintragKonfiguration : IEntityTypeConfiguration<AuditEintrag>
{
    public void Configure(EntityTypeBuilder<AuditEintrag> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).UseIdentityAlwaysColumn();
        builder.Property(a => a.BenutzerName).HasMaxLength(200);
        builder.Property(a => a.Entitaet).HasMaxLength(100);
        builder.Property(a => a.EntitaetId).HasMaxLength(100);
        builder.Property(a => a.Aktion).HasMaxLength(50);
        builder.Property(a => a.Feld).HasMaxLength(100);

        builder.HasIndex(a => a.BelegId);
        builder.HasIndex(a => new { a.Entitaet, a.EntitaetId });
        builder.HasIndex(a => a.Zeitpunkt);
    }
}
