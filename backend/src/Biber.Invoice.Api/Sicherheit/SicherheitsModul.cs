using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Biber.Invoice.Api.Sicherheit;

/// <summary>
/// Standardschema und Autorisierungsrichtlinien. Die Schemata <see cref="AuthSchemata.Cookie"/>
/// und <see cref="AuthSchemata.ApiToken"/> registrieren die Anmelde-Module; solange eines fehlt,
/// greift <see cref="KeineAnmeldungHandler"/> (Anfrage gilt als anonym).
/// </summary>
public sealed class SicherheitsModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddAuthentication(AuthSchemata.Standard)
            .AddPolicyScheme(AuthSchemata.Standard, AuthSchemata.Standard, optionen =>
            {
                optionen.ForwardDefaultSelector = context =>
                {
                    string? autorisierung = context.Request.Headers.Authorization;
                    string ziel = autorisierung?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                        ? AuthSchemata.ApiToken
                        : AuthSchemata.Cookie;

                    // Fehlt das Anmelde-Modul (noch), würde die Weiterleitung jede Anfrage mit 500 beenden.
                    var schemata = context.RequestServices.GetRequiredService<IOptions<AuthenticationOptions>>().Value.SchemeMap;
                    return schemata.ContainsKey(ziel) ? ziel : KeineAnmeldungHandler.Schema;
                };
            })
            .AddScheme<AuthenticationSchemeOptions, KeineAnmeldungHandler>(KeineAnmeldungHandler.Schema, _ => { });

        dienste.AddAuthorizationBuilder()
            .AddPolicy(Richtlinien.Lesen, p => p.RequireRole(nameof(Rolle.Admin), nameof(Rolle.Benutzer), nameof(Rolle.NurLesen)))
            .AddPolicy(Richtlinien.Bearbeiten, p => p.RequireRole(nameof(Rolle.Admin), nameof(Rolle.Benutzer)))
            .AddPolicy(Richtlinien.Admin, p => p.RequireRole(nameof(Rolle.Admin)));

        dienste.AddHttpContextAccessor();
        dienste.AddScoped<IAktuellerBenutzer, AktuellerBenutzer>();
    }
}
