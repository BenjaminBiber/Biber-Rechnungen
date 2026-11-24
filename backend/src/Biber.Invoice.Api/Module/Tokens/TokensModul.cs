using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Tokens;
using Microsoft.AspNetCore.Authentication;

namespace Biber.Invoice.Api.Module.Tokens;

/// <summary>Registriert <see cref="TokenDienst"/>, das Schema <see cref="AuthSchemata.ApiToken"/> und die <see cref="McpRichtlinien"/>.</summary>
public sealed class TokensModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<TokenDienst>();

        dienste.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiTokenHandler>(AuthSchemata.ApiToken, _ => { });

        dienste.AddAuthorizationBuilder()
            .AddPolicy(McpRichtlinien.McpLesen, p => p
                .AddAuthenticationSchemes(AuthSchemata.ApiToken)
                .RequireAuthenticatedUser()
                .RequireClaim(ApiTokenHandler.ScopeClaim, TokenScopes.Lesen))
            .AddPolicy(McpRichtlinien.McpUpload, p => p
                .AddAuthenticationSchemes(AuthSchemata.ApiToken)
                .RequireAuthenticatedUser()
                .RequireClaim(ApiTokenHandler.ScopeClaim, TokenScopes.Upload));
    }
}
