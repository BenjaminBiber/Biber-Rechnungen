using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Auth;

/// <summary>
/// Lokale Anmeldung mit ASP.NET Core Identity: Benutzer, Rollen und Cookie <see cref="CookieName"/>
/// (Schema <see cref="AuthSchemata.Cookie"/>). Passwort ab 12 Zeichen ohne Zeichenklassen-Pflicht,
/// Sperre nach 5 Fehlversuchen für 10 Minuten. Geschützte Aufrufe ohne gültiges Cookie erhalten 401/403
/// statt einer Umleitung.
/// </summary>
public sealed class AuthModul : IDienstModul
{
    public const string CookieName = "biber.sitzung";

    public static readonly TimeSpan CookieLaufzeit = TimeSpan.FromDays(14);

    public static readonly TimeSpan GeraetVertrauenLaufzeit = TimeSpan.FromDays(30);

    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddIdentityCore<Benutzer>(optionen =>
            {
                optionen.Password.RequiredLength = 12;
                optionen.Password.RequiredUniqueChars = 1;
                optionen.Password.RequireDigit = false;
                optionen.Password.RequireLowercase = false;
                optionen.Password.RequireUppercase = false;
                optionen.Password.RequireNonAlphanumeric = false;

                optionen.Lockout.MaxFailedAccessAttempts = 5;
                optionen.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                optionen.Lockout.AllowedForNewUsers = true;

                // Der Benutzername ist die E-Mail-Adresse; deren Format prüft RequireUniqueEmail.
                optionen.User.RequireUniqueEmail = true;
                optionen.User.AllowedUserNameCharacters = "";
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<BiberDbContext>()
            .AddClaimsPrincipalFactory<BenutzerClaimsFactory>()
            .AddSignInManager<BiberSignInManager>()
            .AddErrorDescriber<DeutscheIdentityFehler>()
            .AddDefaultTokenProviders();

        dienste.AddAuthentication().AddIdentityCookies();
        dienste.ConfigureApplicationCookie(optionen =>
        {
            optionen.Cookie.Name = CookieName;
            optionen.Cookie.HttpOnly = true;
            optionen.Cookie.SameSite = SameSiteMode.Lax;
            optionen.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            optionen.ExpireTimeSpan = CookieLaufzeit;
            optionen.SlidingExpiration = true;
            optionen.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = SitzungsPruefung.PruefenAsync,
                OnRedirectToLogin = kontext => StatusOhneUmleitung(kontext, StatusCodes.Status401Unauthorized),
                OnRedirectToAccessDenied = kontext => StatusOhneUmleitung(kontext, StatusCodes.Status403Forbidden),
            };
        });
        dienste.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme, optionen =>
        {
            optionen.ExpireTimeSpan = GeraetVertrauenLaufzeit;
            optionen.Cookie.SameSite = SameSiteMode.Lax;
            optionen.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        dienste.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, optionen =>
        {
            optionen.Cookie.SameSite = SameSiteMode.Lax;
            optionen.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        dienste.Configure<SecurityStampValidatorOptions>(optionen =>
        {
            optionen.ValidationInterval = TimeSpan.FromMinutes(1);
            optionen.OnRefreshingPrincipal = SitzungsPruefung.ClaimsUebernehmenAsync;
        });

        dienste.AddScoped<AnmeldeSpeicher>();
    }

    private static Task StatusOhneUmleitung(RedirectContext<CookieAuthenticationOptions> kontext, int status)
    {
        kontext.Response.StatusCode = status;
        return Task.CompletedTask;
    }
}
