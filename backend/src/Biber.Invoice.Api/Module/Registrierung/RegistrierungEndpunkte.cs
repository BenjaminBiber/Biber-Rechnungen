using Biber.Invoice.Api.Module.Auth;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Benutzer;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Registrierung;
using Microsoft.AspNetCore.Identity;

namespace Biber.Invoice.Api.Module.Registrierung;

/// <summary>
/// Offene Registrierung: <c>POST /api/auth/registrieren</c> antwortet immer 202 (auch bei vergebener E-Mail-Adresse),
/// Freigabe und Ablehnung wartender Konten durch den Admin.
/// </summary>
public sealed class RegistrierungEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        // Program.cs ruft UseRateLimiter nicht auf; die Middleware muss hinter dem Routing und vor den Endpunkten laufen.
        if (app is IApplicationBuilder pipeline)
        {
            pipeline.UseRateLimiter();
        }

        app.MapPost(ApiRouten.Auth.Registrieren, RegistrierenAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RegistrierungModul.BegrenzungsRichtlinie);
        app.MapPost(ApiRouten.Benutzer.Freigeben, FreigebenAsync).RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.Benutzer.Ablehnen, AblehnenAsync).RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<IResult> RegistrierenAsync(
        HttpRequest anfrage, RegistrierungsDienst dienst, CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<RegistrierenDto>(anfrage, ct);
        if (dto is null)
        {
            return AuthHilfen.Ungueltig("", "Ungültiger Inhalt.");
        }

        var fehler = new Dictionary<string, string[]>();
        return await dienst.RegistrierenAsync(dto, fehler, ct) switch
        {
            RegistrierungsErgebnis.Angenommen or RegistrierungsErgebnis.Ignoriert => Results.Accepted(),
            RegistrierungsErgebnis.Geschlossen => Results.Problem(
                statusCode: StatusCodes.Status403Forbidden, title: "Die Registrierung ist geschlossen."),
            RegistrierungsErgebnis.KeinBenutzer => AuthHilfen.Konflikt("Die Ersteinrichtung ist noch nicht abgeschlossen."),
            _ => Results.ValidationProblem(fehler),
        };
    }

    private static async Task<IResult> FreigebenAsync(
        Guid id,
        HttpRequest anfrage,
        RegistrierungsDienst dienst,
        UserManager<Benutzer> userManager,
        CancellationToken ct)
    {
        var dto = await AuthHilfen.LesenAsync<BenutzerFreigebenDto>(anfrage, ct);
        if (dto is null)
        {
            return AuthHilfen.Ungueltig("rolle", "Die Rolle ist nötig.");
        }

        var (ergebnis, benutzer) = await dienst.FreigebenAsync(id, dto.Rolle, ct);
        return ergebnis switch
        {
            FreigabeErgebnis.Ok => AuthHilfen.Ok(await AuthHilfen.DtoAsync(benutzer!, userManager)),
            FreigabeErgebnis.UngueltigeRolle => AuthHilfen.Ungueltig("rolle", "Unbekannte Rolle."),
            FreigabeErgebnis.BereitsFreigegeben => AuthHilfen.Konflikt("Das Konto ist bereits freigegeben."),
            _ => AuthHilfen.NichtGefunden(),
        };
    }

    private static async Task<IResult> AblehnenAsync(Guid id, RegistrierungsDienst dienst, CancellationToken ct) =>
        await dienst.AblehnenAsync(id, ct) switch
        {
            FreigabeErgebnis.Ok => Results.NoContent(),
            FreigabeErgebnis.BereitsFreigegeben => AuthHilfen.Konflikt("Ein freigegebenes Konto kann nicht abgelehnt werden."),
            _ => AuthHilfen.NichtGefunden(),
        };
}
