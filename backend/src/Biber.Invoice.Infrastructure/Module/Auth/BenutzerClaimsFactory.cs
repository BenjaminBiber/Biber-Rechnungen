using System.Security.Claims;
using Biber.Invoice.Domain.Entitaeten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Biber.Invoice.Infrastructure.Module.Auth;

/// <summary>Wie der Standard von Identity, aber mit dem Anzeigenamen statt der E-Mail im Claim <see cref="ClaimTypes.Name"/>.</summary>
public sealed class BenutzerClaimsFactory(
    UserManager<Benutzer> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<Benutzer, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Benutzer user)
    {
        var identitaet = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.Anzeigename))
        {
            foreach (var name in identitaet.FindAll(identitaet.NameClaimType).ToList())
            {
                identitaet.RemoveClaim(name);
            }

            identitaet.AddClaim(new Claim(identitaet.NameClaimType, user.Anzeigename));
        }

        return identitaet;
    }
}
