using System.Text.Encodings.Web;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Module;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biber.Invoice.Infrastructure.Module.Google;

/// <summary>
/// Anmeldung über Google (OAuth 2.0 / OpenID Connect). Wird nur registriert, wenn <c>Google:ClientId</c> und
/// <c>Google:ClientSecret</c> gesetzt sind. Das Ergebnis landet im externen Cookie von Identity; ausgewertet wird
/// es vom Rückruf-Endpunkt. Endpunkte von Google lassen sich für Tests über <c>Google:AuthorizationEndpunkt</c>,
/// <c>Google:TokenEndpunkt</c> und <c>Google:UserInfoEndpunkt</c> ersetzen.
/// </summary>
public sealed class GoogleModul : IDienstModul
{
    /// <summary>Interner Pfad, auf dem die Middleware die Antwort von Google annimmt.</summary>
    public const string RueckrufPfad = "/api/auth/google/signin";

    /// <summary>Claim mit der von Google gemeldeten Bestätigung der E-Mail-Adresse.</summary>
    public const string EmailVerifiziertClaim = "email_verified";

    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        var clientId = konfiguration["Google:ClientId"];
        var clientSecret = konfiguration["Google:ClientSecret"];
        dienste.AddScoped<IAuthStatusErgaenzung, GoogleStatusErgaenzung>();
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return;
        }

        dienste.AddAuthentication().AddOAuth<GoogleOptions, OeffentlicherGoogleHandler>(
            GoogleDefaults.AuthenticationScheme,
            GoogleDefaults.DisplayName,
            optionen =>
            {
                optionen.ClientId = clientId;
                optionen.ClientSecret = clientSecret;
                optionen.SignInScheme = IdentityConstants.ExternalScheme;
                optionen.CallbackPath = RueckrufPfad;
                optionen.AuthorizationEndpoint = konfiguration["Google:AuthorizationEndpunkt"] ?? optionen.AuthorizationEndpoint;
                optionen.TokenEndpoint = konfiguration["Google:TokenEndpunkt"] ?? optionen.TokenEndpoint;
                optionen.UserInformationEndpoint = konfiguration["Google:UserInfoEndpunkt"] ?? optionen.UserInformationEndpoint;
                optionen.ClaimActions.MapJsonKey(EmailVerifiziertClaim, "email_verified");
                optionen.ClaimActions.MapJsonKey(EmailVerifiziertClaim, "verified_email");

                // Abgebrochene oder gefälschte Antworten (z. B. ungültiger state) enden auf der Anmeldeseite statt in 500.
                optionen.Events.OnRemoteFailure = kontext =>
                {
                    kontext.Response.Redirect("/anmelden?fehler=google-unbekannt");
                    kontext.HandleResponse();
                    return Task.CompletedTask;
                };
            });
    }
}

/// <summary>
/// Google-Handler, dessen Redirect-URL aus <c>Anwendung:OeffentlicheUrl</c> gebildet wird (LAN-Domain hinter
/// einem Proxy); ohne diesen Wert gilt die Adresse der Anfrage.
/// </summary>
public sealed class OeffentlicherGoogleHandler(
    IOptionsMonitor<GoogleOptions> optionen,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration konfiguration)
    : GoogleHandler(optionen, logger, encoder)
{
    protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri) =>
        base.BuildChallengeUrl(properties, OeffentlicheRedirectUri() ?? redirectUri);

    protected override Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var oeffentlich = OeffentlicheRedirectUri();
        return base.ExchangeCodeAsync(oeffentlich is null
            ? context
            : new OAuthCodeExchangeContext(context.Properties, context.Code, oeffentlich));
    }

    private string? OeffentlicheRedirectUri()
    {
        var basis = konfiguration["Anwendung:OeffentlicheUrl"];
        return string.IsNullOrWhiteSpace(basis) ? null : basis.TrimEnd('/') + Options.CallbackPath;
    }
}

/// <summary>Google ist aktiv, wenn der Handler konfiguriert und die Einstellung „Google-Anmeldung“ an ist.</summary>
public sealed class GoogleStatusErgaenzung(IAuthenticationSchemeProvider schemata, IEinstellungen einstellungen)
    : IAuthStatusErgaenzung
{
    public async Task ErgaenzenAsync(AuthStatusErgaenzung status, CancellationToken ct)
    {
        status.GoogleAktiv = await GoogleAktivAsync(schemata, einstellungen, ct);
    }

    public static async Task<bool> GoogleAktivAsync(
        IAuthenticationSchemeProvider schemata, IEinstellungen einstellungen, CancellationToken ct) =>
        await schemata.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is not null
        && (await einstellungen.LesenAsync<ZugriffEinstellungen>(ct)).GoogleAnmeldung;
}
