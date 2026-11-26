using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Aufbewahrung;
using Biber.Invoice.Infrastructure.Module.Belege.Schreiben;

namespace Biber.Invoice.Api.Module.Aufbewahrung;

/// <summary>Gelöschte Belege (Admin): falsch zugeordnete Belege trotz Löschsperre ausblenden und wiederherstellen.</summary>
public sealed class GeloeschteBelegeEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Belege.Geloeschte, ListeAsync).RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.Belege.Loeschen, LoeschenAsync).RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.Belege.Wiederherstellen, WiederherstellenAsync).RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<IResult> ListeAsync(GeloeschteBelegeDienst dienst, CancellationToken ct) =>
        TypedResults.Ok(await dienst.ListeAsync(ct));

    private static async Task<IResult> LoeschenAsync(
        Guid id, BelegLoeschenDto anfrage, GeloeschteBelegeDienst dienst, CancellationToken ct)
    {
        try
        {
            return await dienst.LoeschenAsync(id, anfrage, ct) ? TypedResults.NoContent() : NichtGefunden();
        }
        catch (BelegValidierungsAusnahme ex)
        {
            return TypedResults.ValidationProblem(ex.Fehler);
        }
        catch (BelegKonfliktAusnahme ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Konflikt", detail: ex.Message);
        }
    }

    private static async Task<IResult> WiederherstellenAsync(Guid id, GeloeschteBelegeDienst dienst, CancellationToken ct) =>
        await dienst.WiederherstellenAsync(id, ct) ? TypedResults.NoContent() : NichtGefunden();

    private static IResult NichtGefunden() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Beleg nicht gefunden.");
}
