using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Audit;

namespace Biber.Invoice.Api.Module.Audit;

/// <summary><c>GET /api/belege/{id}/historie</c>: Änderungsprotokoll eines Belegs, neueste Einträge zuerst.</summary>
public sealed class HistorieEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Belege.Historie, async (Guid id, HistorieLeser leser, CancellationToken ct) =>
                await leser.LesenAsync(id, ct) is { } historie
                    ? Results.Ok(historie)
                    : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Beleg nicht gefunden"))
            .Produces<AuditEintragDto[]>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(Richtlinien.Lesen);
    }
}
