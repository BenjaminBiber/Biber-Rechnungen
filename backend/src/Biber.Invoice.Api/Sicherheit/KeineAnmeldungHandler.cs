using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Biber.Invoice.Api.Sicherheit;

/// <summary>
/// Rückfallebene des Standardschemas: Solange das eigentliche Anmelde-Schema nicht registriert ist,
/// gilt jede Anfrage als anonym. Anonyme Endpunkte antworten normal, geschützte mit 401.
/// </summary>
public sealed class KeineAnmeldungHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Schema = "KeineAnmeldung";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
