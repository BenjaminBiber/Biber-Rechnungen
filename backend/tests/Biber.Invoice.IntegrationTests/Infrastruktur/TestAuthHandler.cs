using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biber.Invoice.IntegrationTests.Infrastruktur;

/// <summary>
/// Schema „Test“: Die Header <c>X-Test-Rolle</c> und <c>X-Test-Benutzer</c> bestimmen die Claims.
/// Ohne <c>X-Test-Rolle</c> ist die Anfrage anonym (401 bei geschützten Endpunkten).
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Schema = "Test";
    public const string RolleHeader = "X-Test-Rolle";
    public const string BenutzerHeader = "X-Test-Benutzer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolleHeader, out var rolle) || string.IsNullOrWhiteSpace(rolle))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string benutzer = Request.Headers.TryGetValue(BenutzerHeader, out var wert) && !string.IsNullOrWhiteSpace(wert)
            ? wert.ToString()
            : Guid.Empty.ToString();

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, benutzer),
            new(ClaimTypes.Name, $"Test-{rolle}"),
            new(ClaimTypes.Role, rolle.ToString()),
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Schema));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Schema)));
    }
}
