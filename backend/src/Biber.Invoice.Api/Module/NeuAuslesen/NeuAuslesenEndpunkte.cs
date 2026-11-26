using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.NeuAuslesen;

namespace Biber.Invoice.Api.Module.NeuAuslesen;

/// <summary>Einstellungen → Document Intelligence: alle Belege erneut auslesen, Fortschritt abfragen, abbrechen (Admin).</summary>
public sealed class NeuAuslesenEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.NeuAuslesen.Status, StatusAsync)
            .RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.NeuAuslesen.Starten, StartenAsync)
            .RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.NeuAuslesen.Abbrechen, AbbrechenAsync)
            .RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<IResult> StatusAsync(NeuAuslesenDienst dienst, CancellationToken ct) =>
        TypedResults.Ok(await dienst.StatusAsync(ct));

    private static async Task<IResult> StartenAsync(NeuAuslesenDienst dienst, CancellationToken ct) =>
        await dienst.StartenAsync(ct) is { } status
            ? TypedResults.Accepted((string?)null, status)
            : TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Lauf läuft bereits",
                detail: "Es werden bereits alle Belege erneut ausgelesen.");

    private static async Task<IResult> AbbrechenAsync(NeuAuslesenDienst dienst, CancellationToken ct) =>
        await dienst.AbbrechenAsync(ct) is { } status
            ? TypedResults.Ok(status)
            : TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Kein Lauf aktiv",
                detail: "Es läuft gerade kein erneutes Auslesen.");
}
