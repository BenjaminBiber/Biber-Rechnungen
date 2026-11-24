using System.Security.Claims;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Konto;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Auth;
using Microsoft.AspNetCore.Identity;

namespace Biber.Invoice.Api.Module.Auth;

/// <summary>
/// Eigenes Konto: Name und E-Mail, Passwort, TOTP (einrichten → aktivieren → deaktivieren),
/// Wiederherstellungscodes und „Andere Sitzungen abmelden“.
/// </summary>
public sealed class KontoEndpunkte : IEndpunktModul
{
    public const int AnzahlWiederherstellungscodes = 8;

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Konto.Eigenes, LesenAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapPut(ApiRouten.Konto.Eigenes, AendernAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Konto.Passwort, PasswortAendernAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Konto.TotpEinrichten, TotpEinrichtenAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Konto.TotpAktivieren, TotpAktivierenAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Konto.TotpDeaktivieren, TotpDeaktivierenAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Konto.Wiederherstellungscodes, WiederherstellungscodesAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Konto.AndereSitzungenAbmelden, AndereAbmeldenAsync).RequireAuthorization(Richtlinien.Lesen);
    }

    private static async Task<IResult> LesenAsync(
        HttpContext http, UserManager<Benutzer> userManager, AnmeldeSpeicher speicher, CancellationToken ct)
    {
        var benutzer = await userManager.GetUserAsync(http.User);
        return benutzer is null ? Results.Unauthorized() : AuthHilfen.Ok(await KontoAsync(benutzer, userManager, speicher, ct));
    }

    private static async Task<IResult> AendernAsync(
        HttpContext http,
        UserManager<Benutzer> userManager,
        SignInManager<Benutzer> signInManager,
        AnmeldeSpeicher speicher,
        CancellationToken ct)
    {
        var benutzer = await userManager.GetUserAsync(http.User);
        if (benutzer is null)
        {
            return Results.Unauthorized();
        }

        var dto = await AuthHilfen.LesenAsync<KontoAendernDto>(http.Request, ct);
        if (dto is null)
        {
            return AuthHilfen.Ungueltig("", "Ungültiger Inhalt.");
        }

        var fehler = new Dictionary<string, string[]>();
        AuthHilfen.NameUndEmailPruefen(dto.Name, dto.Email, fehler);
        if (fehler.Count > 0)
        {
            return Results.ValidationProblem(fehler);
        }

        benutzer.Anzeigename = dto.Name.Trim();
        benutzer.Email = dto.Email.Trim();
        benutzer.UserName = dto.Email.Trim();
        var ergebnis = await userManager.UpdateAsync(benutzer);
        if (!ergebnis.Succeeded)
        {
            return AuthHilfen.Ungueltig(ergebnis, "email");
        }

        await signInManager.RefreshSignInAsync(benutzer);
        return AuthHilfen.Ok(await KontoAsync(benutzer, userManager, speicher, ct));
    }

    private static async Task<IResult> PasswortAendernAsync(
        HttpContext http,
        UserManager<Benutzer> userManager,
        SignInManager<Benutzer> signInManager,
        AnmeldeSpeicher speicher,
        CancellationToken ct)
    {
        var benutzer = await userManager.GetUserAsync(http.User);
        if (benutzer is null)
        {
            return Results.Unauthorized();
        }

        var dto = await AuthHilfen.LesenAsync<PasswortAendernDto>(http.Request, ct);
        if (dto is null)
        {
            return AuthHilfen.Ungueltig("", "Ungültiger Inhalt.");
        }

        var ergebnis = await userManager.ChangePasswordAsync(benutzer, dto.AltesPasswort ?? "", dto.NeuesPasswort ?? "");
        if (!ergebnis.Succeeded)
        {
            return Results.ValidationProblem(ergebnis.Errors
                .GroupBy(f => f.Code == nameof(IdentityErrorDescriber.PasswordMismatch) ? "altesPasswort" : "neuesPasswort")
                .ToDictionary(g => g.Key, g => g.Select(f => f.Description).ToArray()));
        }

        await speicher.PasswortGeaendertAsync(benutzer.Id, ct);
        await speicher.NurSitzungBehaltenAsync(benutzer.Id, Sitzung(http), ct);
        await signInManager.RefreshSignInAsync(benutzer);
        return Results.NoContent();
    }

    private static async Task<IResult> TotpEinrichtenAsync(
        HttpContext http, UserManager<Benutzer> userManager, SignInManager<Benutzer> signInManager)
    {
        var benutzer = await userManager.GetUserAsync(http.User);
        if (benutzer is null)
        {
            return Results.Unauthorized();
        }

        if (benutzer.TwoFactorEnabled)
        {
            return AuthHilfen.Konflikt("TOTP ist bereits aktiv.");
        }

        await userManager.ResetAuthenticatorKeyAsync(benutzer);
        var schluessel = await userManager.GetAuthenticatorKeyAsync(benutzer) ?? "";
        await signInManager.RefreshSignInAsync(benutzer);
        return AuthHilfen.Ok(new TotpEinrichtungDto(schluessel, TotpSchluessel.OtpAuthUri(benutzer.Email ?? "", schluessel)));
    }

    private static async Task<IResult> TotpAktivierenAsync(
        HttpContext http,
        UserManager<Benutzer> userManager,
        SignInManager<Benutzer> signInManager,
        CancellationToken ct)
    {
        var benutzer = await userManager.GetUserAsync(http.User);
        if (benutzer is null)
        {
            return Results.Unauthorized();
        }

        var dto = await AuthHilfen.LesenAsync<CodeDto>(http.Request, ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Code))
        {
            return AuthHilfen.Ungueltig("code", "Der Code darf nicht leer sein.");
        }

        if (benutzer.TwoFactorEnabled)
        {
            return AuthHilfen.Konflikt("TOTP ist bereits aktiv.");
        }

        if (string.IsNullOrEmpty(await userManager.GetAuthenticatorKeyAsync(benutzer)))
        {
            return AuthHilfen.Konflikt("TOTP wurde noch nicht eingerichtet.");
        }

        var gueltig = await userManager.VerifyTwoFactorTokenAsync(
            benutzer, userManager.Options.Tokens.AuthenticatorTokenProvider, AuthEndpunkte.Code(dto.Code));
        if (!gueltig)
        {
            return AuthHilfen.Ungueltig("code", "Der Code ist ungültig.");
        }

        await userManager.SetTwoFactorEnabledAsync(benutzer, true);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(benutzer, AnzahlWiederherstellungscodes);
        await signInManager.RefreshSignInAsync(benutzer);
        return AuthHilfen.Ok(new WiederherstellungscodesDto([.. codes ?? []]));
    }

    private static async Task<IResult> TotpDeaktivierenAsync(
        HttpContext http, UserManager<Benutzer> userManager, SignInManager<Benutzer> signInManager)
    {
        var benutzer = await userManager.GetUserAsync(http.User);
        if (benutzer is null)
        {
            return Results.Unauthorized();
        }

        await userManager.SetTwoFactorEnabledAsync(benutzer, false);
        await userManager.ResetAuthenticatorKeyAsync(benutzer);
        await userManager.GenerateNewTwoFactorRecoveryCodesAsync(benutzer, 0);
        await signInManager.ForgetTwoFactorClientAsync();
        await signInManager.RefreshSignInAsync(benutzer);
        return Results.NoContent();
    }

    private static async Task<IResult> WiederherstellungscodesAsync(
        HttpContext http, UserManager<Benutzer> userManager)
    {
        var benutzer = await userManager.GetUserAsync(http.User);
        if (benutzer is null)
        {
            return Results.Unauthorized();
        }

        if (!benutzer.TwoFactorEnabled)
        {
            return AuthHilfen.Konflikt("Wiederherstellungscodes gibt es nur mit aktivem TOTP.");
        }

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(benutzer, AnzahlWiederherstellungscodes);
        return AuthHilfen.Ok(new WiederherstellungscodesDto([.. codes ?? []]));
    }

    private static async Task<IResult> AndereAbmeldenAsync(
        HttpContext http, UserManager<Benutzer> userManager, AnmeldeSpeicher speicher, CancellationToken ct)
    {
        var benutzer = await userManager.GetUserAsync(http.User);
        if (benutzer is null)
        {
            return Results.Unauthorized();
        }

        await speicher.NurSitzungBehaltenAsync(benutzer.Id, Sitzung(http), ct);
        return Results.NoContent();
    }

    private static string? Sitzung(HttpContext http) => http.User.FindFirstValue(BiberSignInManager.SitzungClaim);

    private static async Task<KontoDto> KontoAsync(
        Benutzer benutzer, UserManager<Benutzer> userManager, AnmeldeSpeicher speicher, CancellationToken ct)
    {
        var logins = await userManager.GetLoginsAsync(benutzer);
        return new KontoDto(
            benutzer.Anzeigename,
            benutzer.Email ?? "",
            await speicher.PasswortGeaendertAmAsync(benutzer.Id, ct),
            benutzer.TwoFactorEnabled,
            await userManager.CountRecoveryCodesAsync(benutzer),
            logins.Any(l => l.LoginProvider == AuthHilfen.AnbieterGoogle),
            (await speicher.SitzungenAsync(benutzer.Id, ct)).Count);
    }
}
