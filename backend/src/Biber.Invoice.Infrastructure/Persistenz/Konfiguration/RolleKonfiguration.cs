using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Biber.Invoice.Infrastructure.Persistenz.Konfiguration;

/// <summary>Legt die Rollen aus <see cref="Rolle"/> mit festen IDs an.</summary>
public sealed class RolleKonfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.HasData(
            Anlegen(SystemIds.RolleAdmin, Rolle.Admin),
            Anlegen(SystemIds.RolleBenutzer, Rolle.Benutzer),
            Anlegen(SystemIds.RolleNurLesen, Rolle.NurLesen));
    }

    private static IdentityRole<Guid> Anlegen(Guid id, Rolle rolle) => new()
    {
        Id = id,
        Name = rolle.ToString(),
        NormalizedName = rolle.ToString().ToUpperInvariant(),
        ConcurrencyStamp = id.ToString(),
    };
}
