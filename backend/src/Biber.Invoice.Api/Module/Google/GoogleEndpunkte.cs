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

    /// <summary>Eintrag im geschützten state: Id des Benutzers, der die Verknüpfung gestartet hat.</summary>
    private const string VerknuepfenFuer = "biber.verknuepfen";

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

        Benutzer? aktueller = null;
        if (verknuepfen == true)
        {
            aktueller = http.User.Identity?.IsAuthenticated == true ? await userManager.GetUserAsync(http.User) : null;
            if (aktueller is null)
            {
                return Results.Unauthorized();
            }
        }

        var eigenschaften = signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, ApiRouten.Auth.GoogleRueckruf, aktueller?.Id.ToString());

        // Google soll nie stillschweigend das gerade im Browser aktive Google-Konto nehmen.
        eigenschaften.SetParameter(GoogleChallengeProperties.PromptParameterKey, "select_account");
        if (aktueller is not null)
        {
            // Der Modus steckt im geschützten state, nicht im Cookie: ein Verknüpfen endet nie in Anmeldung
            // oder Registrierung, auch wenn das Anmelde-Cookie den Rückweg von Google nicht übersteht.
            eigenschaften.Items[VerknuepfenFuer] = aktueller.Id.ToString();
            if (!string.IsNullOrEmpty(aktueller.Email))
            {
                eigenschaften.SetParameter(GoogleChallengeProperties.LoginHintKey, aktueller.Email);
            }
        }

        return Results.Challenge(eigenschaften, [GoogleDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> RueckrufAsync(
        HttpContext http,
        SignInManager<Benutzer> signInManager,
        UserManager<Benutzer> userManager,
        IEinstellungen einstellungen,
        TimeProvider zeit,
        IEnumerable<IBenachrichtigungsDienst> benachrichtigungen,
        CancellationToken ct)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        await http.SignOutAsync(IdentityConstants.ExternalScheme);
        if (info is null)
        {
            return Results.Redirect("/anmelden?fehler=google-unbekannt");
        }

        var verknuepfter = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (info.AuthenticationProperties?.Items.TryGetValue(VerknuepfenFuer, out var fuer) == true)
        {
            return await VerknuepfenAsync(http, userManager, info, verknuepfter, fuer);
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

    /// <summary>
    /// Verknüpft das Google-Konto mit dem Benutzer, der die Verknüpfung gestartet hat. Ist dieser nicht mehr
    /// (oder ein anderer) angemeldet, wird nichts verknüpft – weder Anmeldung noch Registrierung.
    /// </summary>
    private static async Task<IResult> VerknuepfenAsync(
        HttpContext http,
        UserManager<Benutzer> userManager,
        ExternalLoginInfo info,
        Benutzer? verknuepfter,
        string? fuer)
    {
        var aktueller = http.User.Identity?.IsAuthenticated == true ? await userManager.GetUserAsync(http.User) : null;
        if (aktueller is null || aktueller.Id.ToString() != fuer)
        {
            return Results.Redirect("/einstellungen/konto?google=fehlgeschlagen");
        }

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
