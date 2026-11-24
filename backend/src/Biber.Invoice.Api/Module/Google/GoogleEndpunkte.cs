using System.Security.Claims;
using Biber.Invoice.Api.Module.Auth;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;

namespace Biber.Invoice.Api.Module.Google;

/// <summary>
/// Anmeldung und Kontoverknüpfung über Google: <c>start</c> löst die Challenge aus, <c>rueckruf</c> wertet das
/// externe Ergebnis aus und leitet auf das Frontend um. Google gilt als zweiter Faktor (kein TOTP).
/// </summary>
public sealed class GoogleEndpunkte : IEndpunktModul
{
    private const string Anmeldemethode = "google";

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Auth.GoogleStart, StartAsync).AllowAnonymous();
        app.MapGet(ApiRouten.Auth.GoogleRueckruf, RueckrufAsync).AllowAnonymous();
    }

    private static async Task<IResult> StartAsync(
        HttpContext http,
        IAuthenticationSchemeProvider schemata,
        IEinstellungen einstellungen,
        SignInManager<Benutzer> signInManager,
        UserManager<Benutzer> userManager,
        bool? verknuepfen,
        CancellationToken ct)
    {
        if (!await GoogleStatusErgaenzung.GoogleAktivAsync(schemata, einstellungen, ct))
        {
            return AuthHilfen.NichtGefunden();
        }

        string? benutzerId = null;
        if (verknuepfen == true)
        {
            benutzerId = http.User.Identity?.IsAuthenticated == true ? userManager.GetUserId(http.User) : null;
            if (benutzerId is null)
            {
                return Results.Unauthorized();
            }
        }

        var ziel = ApiRouten.Auth.GoogleRueckruf + (benutzerId is null ? "" : "?verknuepfen=true");
        var eigenschaften = signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, ziel, benutzerId);
        return Results.Challenge(eigenschaften, [GoogleDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> RueckrufAsync(
        HttpContext http,
        SignInManager<Benutzer> signInManager,
        UserManager<Benutzer> userManager,
        IEinstellungen einstellungen,
        TimeProvider zeit,
        IEnumerable<IBenachrichtigungsDienst> benachrichtigungen,
        bool? verknuepfen,
        CancellationToken ct)
    {
        var aktueller = verknuepfen == true && http.User.Identity?.IsAuthenticated == true
            ? await userManager.GetUserAsync(http.User)
            : null;
        var info = await signInManager.GetExternalLoginInfoAsync(aktueller?.Id.ToString());
        await http.SignOutAsync(IdentityConstants.ExternalScheme);
        if (info is null)
        {
            return Results.Redirect("/anmelden?fehler=google-unbekannt");
        }

        var verknuepfter = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (aktueller is not null)
        {
            if (verknuepfter is not null)
            {
                return Results.Redirect(verknuepfter.Id == aktueller.Id
                    ? "/einstellungen/konto?google=verknuepft"
                    : "/einstellungen/konto?google=vergeben");
            }

            var ergebnis = await userManager.AddLoginAsync(aktueller, Login(info));
            return Results.Redirect(ergebnis.Succeeded
                ? "/einstellungen/konto?google=verknuepft"
                : "/einstellungen/konto?google=vergeben");
        }

        if (verknuepfter is not null)
        {
            return await AnmeldenAsync(verknuepfter, signInManager, userManager);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email)?.Trim();
        var verifiziert = string.Equals(
            info.Principal.FindFirstValue(GoogleModul.EmailVerifiziertClaim), "true", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(email) || !verifiziert)
        {
            return Results.Redirect("/anmelden?fehler=google-unbekannt");
        }

        var perEmail = await userManager.FindByEmailAsync(email);
        if (perEmail is not null)
        {
            if (!perEmail.Freigegeben || await userManager.IsLockedOutAsync(perEmail))
            {
                return await AnmeldenAsync(perEmail, signInManager, userManager);
            }

            if (!(await userManager.AddLoginAsync(perEmail, Login(info))).Succeeded)
            {
                return Results.Redirect("/anmelden?fehler=google-unbekannt");
            }

            return await AnmeldenAsync(perEmail, signInManager, userManager);
        }

        if (!(await einstellungen.LesenAsync<ZugriffEinstellungen>(ct)).OffeneRegistrierung)
        {
            return Results.Redirect("/anmelden?fehler=google-unbekannt");
        }

        var name = info.Principal.FindFirstValue(ClaimTypes.Name);
        var neuer = AuthHilfen.NeuerBenutzer(
            string.IsNullOrWhiteSpace(name) ? email : name, email, freigegeben: false, zeit.GetUtcNow());
        if (!(await userManager.CreateAsync(neuer)).Succeeded
            || !(await userManager.AddLoginAsync(neuer, Login(info))).Succeeded)
        {
            return Results.Redirect("/anmelden?fehler=google-unbekannt");
        }

        foreach (var dienst in benachrichtigungen)
        {
            await dienst.MeldenAsync(
                new NeueBenachrichtigung(
                    BenachrichtigungArt.KontoWartetAufFreigabe,
                    Schweregrad.Info,
                    "Konto wartet auf Freigabe",
                    $"{neuer.Anzeigename} ({email}) hat sich mit Google registriert.",
                    Link: "/einstellungen/benutzer",
                    NurAdmins: true,
                    EntdoppelungsSchluessel: $"konto-freigabe:{neuer.Id}"),
                ct);
        }

        return Results.Redirect("/anmelden?schritt=freigabe");
    }

    /// <summary>Meldet <paramref name="benutzer"/> ohne TOTP an, sofern freigegeben und nicht gesperrt.</summary>
    private static async Task<IResult> AnmeldenAsync(
        Benutzer benutzer, SignInManager<Benutzer> signInManager, UserManager<Benutzer> userManager)
    {
        if (await userManager.IsLockedOutAsync(benutzer))
        {
            return Results.Redirect("/anmelden?fehler=gesperrt");
        }

        if (!benutzer.Freigegeben)
        {
            return Results.Redirect("/anmelden?schritt=freigabe");
        }

        await signInManager.SignInAsync(benutzer, isPersistent: false, authenticationMethod: Anmeldemethode);
        return Results.Redirect("/");
    }

    private static UserLoginInfo Login(ExternalLoginInfo info) =>
        new(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName);
}
