using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Auth;

/// <summary>
/// Prüft das Anmelde-Cookie bei jeder Anfrage: erst den Security-Stamp (Identity), dann ob die Sitzung
/// (<see cref="BiberSignInManager.SitzungClaim"/>) noch im <see cref="AnmeldeSpeicher"/> steht. Unbekannte
/// Sitzungen werden abgelehnt und das Cookie gelöscht. Danach wird die letzte Aktivität vermerkt.
/// </summary>
public static class SitzungsPruefung
{
    public static async Task PruefenAsync(CookieValidatePrincipalContext kontext)
    {
        await SecurityStampValidator.ValidatePrincipalAsync(kontext);
        if (kontext.Principal is null)
        {
            return;
        }

        var dienste = kontext.HttpContext.RequestServices;
        var ct = kontext.HttpContext.RequestAborted;
        var speicher = dienste.GetRequiredService<AnmeldeSpeicher>();
        var sitzung = kontext.Principal.FindFirstValue(BiberSignInManager.SitzungClaim);
        if (!Guid.TryParse(kontext.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var benutzerId)
            || sitzung is null
            || !await speicher.SitzungBekanntAsync(benutzerId, sitzung, ct))
        {
            kontext.RejectPrincipal();
            await kontext.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        await speicher.AktivitaetMerkenAsync(benutzerId, erzwingen: false, ct);
    }

    /// <summary>Übernimmt Sitzung und Anmeldemethode, wenn Identity das Cookie nach der Stamp-Prüfung neu ausstellt.</summary>
    public static Task ClaimsUebernehmenAsync(SecurityStampRefreshingPrincipalContext kontext)
    {
        if (kontext.NewPrincipal?.Identity is ClaimsIdentity neu && kontext.CurrentPrincipal is { } alt)
        {
            foreach (var claim in alt.Claims.Where(c => BiberSignInManager.ErhalteneClaims.Contains(c.Type)))
            {
                if (!neu.HasClaim(c => c.Type == claim.Type))
                {
                    neu.AddClaim(new Claim(claim.Type, claim.Value));
                }
            }
        }

        return Task.CompletedTask;
    }
}
