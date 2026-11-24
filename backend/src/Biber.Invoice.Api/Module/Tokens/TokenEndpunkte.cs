using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Tokens;

namespace Biber.Invoice.Api.Module.Tokens;

/// <summary>Eigene API-Tokens: Liste, Erzeugen (Klartext nur in dieser Antwort) und Widerrufen.</summary>
public sealed class TokenEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Tokens.Liste, ListeAsync)
            .RequireAuthorization(Richtlinien.Lesen);

        // NurLesen darf Tokens anlegen, aber nur mit Scope „lesen“ (Prüfung im Endpunkt).
        app.MapPost(ApiRouten.Tokens.Liste, ErzeugenAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Tokens.Widerrufen, WiderrufenAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten);
    }

    private static async Task<IResult> ListeAsync(TokenDienst dienst, IAktuellerBenutzer benutzer, CancellationToken ct) =>
        benutzer.Id is { } id
            ? TypedResults.Ok(await dienst.ListeAsync(id, ct))
            : TypedResults.Unauthorized();

    private static async Task<IResult> ErzeugenAsync(
        ApiTokenAnlegenDto anlegen, TokenDienst dienst, IAktuellerBenutzer benutzer, CancellationToken ct)
    {
        if (benutzer.Id is not { } id)
        {
            return TypedResults.Unauthorized();
        }

        bool darfBearbeiten = benutzer.HatRolle(Rolle.Admin) || benutzer.HatRolle(Rolle.Benutzer);
        if (!darfBearbeiten && (anlegen.Scopes ?? []).Any(s => s != TokenScopes.Lesen))
        {
            return TypedResults.Problem(
                detail: "Mit der Rolle „Nur lesen“ sind nur Tokens mit Scope „lesen“ erlaubt.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var ergebnis = await dienst.ErzeugenAsync(id, anlegen, ct);
        if (ergebnis.Erstellt is { } erstellt)
        {
            return TypedResults.Created($"{ApiRouten.Tokens.Liste}/{erstellt.Token.Id}", erstellt);
        }

        return ergebnis.Feld is { } feld
            ? TypedResults.ValidationProblem(new Dictionary<string, string[]> { [feld] = [ergebnis.Fehler!] })
            : TypedResults.Problem(detail: ergebnis.Fehler, statusCode: StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> WiderrufenAsync(
        Guid id, TokenDienst dienst, IAktuellerBenutzer benutzer, CancellationToken ct)
    {
        if (benutzer.Id is not { } benutzerId)
        {
            return TypedResults.Unauthorized();
        }

        return await dienst.WiderrufenAsync(id, benutzerId, benutzer.HatRolle(Rolle.Admin), ct)
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }
}
