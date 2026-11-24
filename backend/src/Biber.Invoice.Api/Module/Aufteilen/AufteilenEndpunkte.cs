using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Aufteilen;

namespace Biber.Invoice.Api.Module.Aufteilen;

/// <summary>Beleg nach Seiten in neue Belege aufteilen.</summary>
public sealed class AufteilenEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRouten.Belege.Aufteilen, async Task<IResult> (Guid id, AufteilenDto anfrage, IAufteilenDienst dienst, CancellationToken ct) =>
            {
                try
                {
                    return await dienst.AufteilenAsync(id, anfrage, ct) is { } vorgaenge
                        ? TypedResults.Ok(vorgaenge)
                        : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Beleg nicht gefunden.");
                }
                catch (AufteilenValidierungsAusnahme ex)
                {
                    return TypedResults.ValidationProblem(ex.Fehler);
                }
                catch (AufteilenKonfliktAusnahme ex)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Konflikt", detail: ex.Message);
                }
            })
            .RequireAuthorization(Richtlinien.Bearbeiten);
    }
}
