using System.Security.Claims;
using Biber.Invoice.Domain.Entitaeten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biber.Invoice.Infrastructure.Module.Auth;

/// <summary>
/// <see cref="SignInManager{TUser}"/>, das jeder neuen Anmeldung eine Sitzung (Claim <see cref="SitzungClaim"/>)
/// im <see cref="AnmeldeSpeicher"/> gibt. Beim Erneuern des Cookies bleibt die Sitzung erhalten.
/// </summary>
public sealed class BiberSignInManager(
    UserManager<Benutzer> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<Benutzer> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<Benutzer>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<Benutzer> confirmation,
    AnmeldeSpeicher speicher)
    : SignInManager<Benutzer>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public const string SitzungClaim = "sid";

    /// <summary>Claims, die beim Erneuern des Cookies erhalten bleiben.</summary>
    public static readonly string[] ErhalteneClaims = [SitzungClaim, "amr", ClaimTypes.AuthenticationMethod];

    public override async Task SignInWithClaimsAsync(
        Benutzer user, AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
    {
        var claims = additionalClaims.ToList();
        if (!claims.Exists(c => c.Type == SitzungClaim))
        {
            var ct = Context.RequestAborted;
            var sitzung = await speicher.SitzungAnlegenAsync(user.Id, Geraet.Kurzform(Context.Request.Headers.UserAgent), ct);
            claims.Add(new Claim(SitzungClaim, sitzung));
            await speicher.AktivitaetMerkenAsync(user.Id, erzwingen: true, ct);
        }

        await base.SignInWithClaimsAsync(user, authenticationProperties, claims);
    }

    /// <summary>Stellt das Cookie mit aktuellen Claims neu aus und behält Sitzung und Anmeldemethode.</summary>
    public override async Task RefreshSignInAsync(Benutzer user)
    {
        var auth = await Context.AuthenticateAsync(AuthenticationScheme);
        if (auth.Principal is null || UserManager.GetUserId(auth.Principal) != user.Id.ToString())
        {
            return;
        }

        var claims = auth.Principal.Claims.Where(c => ErhalteneClaims.Contains(c.Type)).ToList();
        await SignInWithClaimsAsync(user, auth.Properties, claims);
    }
}
