using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Benachrichtigungen;

namespace Biber.Invoice.Api.Module.Benachrichtigungen;

/// <summary>Glocke: Liste, Zähler und „gelesen“ für den angemeldeten Benutzer.</summary>
public sealed class BenachrichtigungEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Benachrichtigungen.Liste, ListeAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Benachrichtigungen.Zaehler, ZaehlerAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Benachrichtigungen.Gelesen, GelesenAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Benachrichtigungen.AlleGelesen, AlleGelesenAsync)
            .RequireAuthorization(Richtlinien.Lesen);
    }

    private static async Task<IResult> ListeAsync(
        BenachrichtigungsAbfrage abfrage, CancellationToken ct, bool nurUngelesen = false, int seite = 1)
    {
        if (seite < 1)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["seite"] = ["Die Seite muss mindestens 1 sein."],
            });
        }

        SeiteDto<BenachrichtigungDto> ergebnis = await abfrage.ListeAsync(nurUngelesen, seite, ct);
        return TypedResults.Ok(ergebnis);
    }

    private static async Task<IResult> ZaehlerAsync(BenachrichtigungsAbfrage abfrage, CancellationToken ct) =>
        TypedResults.Ok(await abfrage.ZaehlerAsync(ct));

    private static async Task<IResult> GelesenAsync(Guid id, BenachrichtigungsAbfrage abfrage, CancellationToken ct) =>
        await abfrage.MarkiereGelesenAsync(id, ct)
            ? TypedResults.NoContent()
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);

    private static async Task<IResult> AlleGelesenAsync(BenachrichtigungsAbfrage abfrage, CancellationToken ct)
    {
        await abfrage.MarkiereAlleGelesenAsync(ct);
        return TypedResults.NoContent();
    }
}
