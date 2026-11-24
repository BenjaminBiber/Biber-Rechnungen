using System.Security.Claims;
using System.Text.Encodings.Web;
using Biber.Invoice.Infrastructure.Module.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Biber.Invoice.Api.Module.Tokens;

/// <summary>
/// Schema <see cref="Contracts.AuthSchemata.ApiToken"/>: meldet <c>Authorization: Bearer brk_…</c> an.
/// Gilt nur unter <see cref="McpPfad"/>; Tokens öffnen die REST-API nicht.
/// </summary>
public sealed class ApiTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TokenDienst tokenDienst)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string McpPfad = "/mcp";
    public const string ScopeClaim = "scope";
    public const string TokenIdClaim = "token_id";
    public const string TokenNameClaim = "token_name";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Path.StartsWithSegments(McpPfad, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string? autorisierung = Request.Headers.Authorization;
        if (autorisierung is null || !autorisierung.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        string klartext = autorisierung["Bearer ".Length..].Trim();
        var geprueft = await tokenDienst.PruefenAsync(klartext, Context.RequestAborted);
        if (geprueft is null)
        {
            return AuthenticateResult.Fail("Ungültiges API-Token.");
        }

        var (token, benutzer, rollen) = geprueft;
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, benutzer.Id.ToString()),
            new(ClaimTypes.Name, benutzer.Anzeigename),
            new(TokenIdClaim, token.Id.ToString()),
            new(TokenNameClaim, token.Name),
        ];
        claims.AddRange(rollen.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(token.Scopes.Select(s => new Claim(ScopeClaim, s)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
