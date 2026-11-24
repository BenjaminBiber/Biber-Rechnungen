using System.Data;
using System.Security.Claims;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Auth;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Api.Module.Auth;

/// <summary>
/// Anmeldestatus, Ersteinrichtung des ersten Admins, Anmeldung mit Passwort und TOTP
/// bzw. Wiederherstellungscode und Abmelden.
/// </summary>
public sealed class AuthEndpunkte : IEndpunktModul
{
    /// <summary>Merkt sich zwischen Passwort und TOTP-Code, ob „Angemeldet bleiben“ gewählt war.</summary>
    internal const string AngemeldetBleibenCookie = "biber.angemeldet-bleiben";

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Auth.Status, StatusAsync).AllowAnonymous();
        app.MapGet(ApiRouten.Auth.TotpVorschlag, TotpVorschlagAsync).AllowAnonymous();
        app.MapPost(ApiRouten.Auth.Ersteinrichtung, ErsteinrichtungAsync).AllowAnonymous();
        app.MapPost(ApiRouten.Auth.Anmelden, AnmeldenAsync).AllowAnonymous();
        app.MapPost(ApiRouten.Auth.Totp, TotpAsync).AllowAnonymous();
        app.MapPost(ApiRouten.Auth.Wiederherstellungscode, WiederherstellungscodeAsync).AllowAnonymous();
        app.MapPost(ApiRouten.Auth.Abmelden, AbmeldenAsync).RequireAuthorization(Richtlinien.Lesen);
    }

    private static async Task<IResult> StatusAsync(
        HttpContext http,
        BiberDbContext db,
        UserManager<Benutzer> userManager,
        IEnumerable<IAuthStatusErgaenzung> ergaenzungen,
        CancellationToken ct)
    {
        var eingerichtet = await db.Users.AnyAsync(ct);
        var benutzer = http.User.Identity?.IsAuthenticated == true ? await userManager.GetUserAsync(http.User) : null;
        var ergaenzung = new AuthStatusErgaenzung();
        foreach (var e in ergaenzungen)
        {
            await e.ErgaenzenAsync(ergaenzung, ct);
        }

        return AuthHilfen.Ok(new AuthStatusDto(
            eingerichtet,
            benutzer is not null,
            benutzer is null ? null : await AuthHilfen.DtoAsync(benutzer, userManager),
            ergaenzung.RegistrierungOffen,
            ergaenzung.GoogleAktiv));
    }

    private static async Task<IResult> TotpVorschlagAsync(BiberDbContext db, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return AuthHilfen.Konflikt("Die Ersteinrichtung ist bereits abgeschlossen.");
        }

        var schluessel = TotpSchluessel.Erzeugen();
        return AuthHilfen.Ok(new TotpEinrichtungDto(schluessel, TotpSchluessel.OtpAuthUri("Admin", schluessel)));
    }

    private static async Task<IResult> ErsteinrichtungAsync(
        HttpRequest anfrage,
        BiberDbContext db,
        UserManager<Benutzer> userManager,
        IUserStore<Benutzer> store,
        SignInManager<Benutzer> signInManager,
        TimeProvider zeit,
        AnmeldeSpeicher speicher,
        CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<ErsteinrichtungDto>(anfrage, ct);
        if (dto is null)
        {
            return AuthHilfen.Ungueltig("", "Ungültiger Inhalt.");
        }

        var fehler = new Dictionary<string, string[]>();
        AuthHilfen.NameUndEmailPruefen(dto.Name, dto.Email, fehler);
        var schluessel = TotpSchluessel.Normalisieren(dto.TotpSchluessel);
        if (schluessel is null)
        {
            fehler["totpSchluessel"] = ["Der TOTP-Schlüssel ist ungültig."];
        }

        if (fehler.Count > 0)
        {
            return Results.ValidationProblem(fehler);
        }

        await using var transaktion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.Users.AnyAsync(ct))
        {
            return AuthHilfen.Konflikt("Die Ersteinrichtung ist bereits abgeschlossen.");
        }

        var benutzer = AuthHilfen.NeuerBenutzer(dto.Name, dto.Email, freigegeben: true, zeit.GetUtcNow());
        var ergebnis = await userManager.CreateAsync(benutzer, dto.Passwort ?? "");
        if (!ergebnis.Succeeded)
        {
            return AuthHilfen.Ungueltig(ergebnis, "passwort");
        }

        await userManager.AddToRoleAsync(benutzer, nameof(Rolle.Admin));
        await ((IUserAuthenticatorKeyStore<Benutzer>)store).SetAuthenticatorKeyAsync(benutzer, schluessel!, ct);
        await userManager.SetTwoFactorEnabledAsync(benutzer, true);
        await speicher.PasswortGeaendertAsync(benutzer.Id, ct);
        await transaktion.CommitAsync(ct);

        await signInManager.SignInAsync(benutzer, isPersistent: false, authenticationMethod: "pwd");
        return Results.Json(await AuthHilfen.DtoAsync(benutzer, userManager), AuthHilfen.Json, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> AnmeldenAsync(
        HttpContext http,
        UserManager<Benutzer> userManager,
        SignInManager<Benutzer> signInManager,
        CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<AnmeldenDto>(http.Request, ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrEmpty(dto.Passwort))
        {
            return AuthHilfen.Ungueltig("", "E-Mail-Adresse und Passwort sind nötig.");
        }

        var benutzer = await userManager.FindByEmailAsync(dto.Email.Trim());
        if (benutzer is null)
        {
            // Gleicher Aufwand wie bei einem bekannten Konto, damit die Antwortzeit nichts verrät.
            userManager.PasswordHasher.HashPassword(new Benutzer(), dto.Passwort);
            return Ergebnis(AnmeldeStatus.Ungueltig);
        }

        if (!benutzer.Freigegeben)
        {
            var pruefung = await signInManager.CheckPasswordSignInAsync(benutzer, dto.Passwort, lockoutOnFailure: true);
            return Ergebnis(pruefung switch
            {
                { Succeeded: true } => AnmeldeStatus.WartetAufFreigabe,
                { IsLockedOut: true } => AnmeldeStatus.Gesperrt,
                _ => AnmeldeStatus.Ungueltig,
            });
        }

        var ergebnis = await signInManager.PasswordSignInAsync(benutzer, dto.Passwort, dto.AngemeldetBleiben, lockoutOnFailure: true);
        if (ergebnis.RequiresTwoFactor)
        {
            if (dto.AngemeldetBleiben)
            {
                http.Response.Cookies.Append(AngemeldetBleibenCookie, "1", new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = http.Request.IsHttps,
                });
            }
            else
            {
                http.Response.Cookies.Delete(AngemeldetBleibenCookie);
            }
        }

        return Ergebnis(Status(ergebnis));
    }

    private static async Task<IResult> TotpAsync(HttpContext http, SignInManager<Benutzer> signInManager, CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<TotpDto>(http.Request, ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Code))
        {
            return AuthHilfen.Ungueltig("code", "Der Code darf nicht leer sein.");
        }

        var angemeldetBleiben = http.Request.Cookies.ContainsKey(AngemeldetBleibenCookie);
        var ergebnis = await signInManager.TwoFactorAuthenticatorSignInAsync(
            Code(dto.Code), angemeldetBleiben, rememberClient: dto.GeraetVertrauen);
        if (ergebnis.Succeeded)
        {
            http.Response.Cookies.Delete(AngemeldetBleibenCookie);
        }

        return Ergebnis(Status(ergebnis));
    }

    private static async Task<IResult> WiederherstellungscodeAsync(
        HttpContext http, SignInManager<Benutzer> signInManager, CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<CodeDto>(http.Request, ct);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Code))
        {
            return AuthHilfen.Ungueltig("code", "Der Code darf nicht leer sein.");
        }

        var ergebnis = await signInManager.TwoFactorRecoveryCodeSignInAsync(dto.Code.Trim().ToUpperInvariant());
        if (ergebnis.Succeeded)
        {
            http.Response.Cookies.Delete(AngemeldetBleibenCookie);
        }

        return Ergebnis(Status(ergebnis));
    }

    private static async Task<IResult> AbmeldenAsync(
        HttpContext http,
        SignInManager<Benutzer> signInManager,
        AnmeldeSpeicher speicher,
        CancellationToken ct)
    {
        var sitzung = http.User.FindFirstValue(BiberSignInManager.SitzungClaim);
        if (sitzung is not null && Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var benutzerId))
        {
            await speicher.SitzungEntfernenAsync(benutzerId, sitzung, ct);
        }

        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static AnmeldeStatus Status(SignInResult ergebnis) => ergebnis switch
    {
        { Succeeded: true } => AnmeldeStatus.Ok,
        { RequiresTwoFactor: true } => AnmeldeStatus.TotpErforderlich,
        { IsLockedOut: true } => AnmeldeStatus.Gesperrt,
        _ => AnmeldeStatus.Ungueltig,
    };

    /// <summary>TOTP-Code ohne Leerzeichen, wie ihn manche Apps anzeigen („123 456“).</summary>
    internal static string Code(string code) => code.Replace(" ", "", StringComparison.Ordinal).Trim();

    private static IResult Ergebnis(AnmeldeStatus status) => AuthHilfen.Ok(new AnmeldeErgebnisDto(status));
}
