using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;

namespace Biber.Invoice.Api.Module.AutoMatching;

/// <summary><c>GET /api/matching/auto-status</c>: Trainingsstand je vorhandenem Auto-Matching-Modell.</summary>
public sealed class AutoMatchingEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Matching.AutoStatus, async (IAutoMatchingDienst dienst, CancellationToken ct) =>
                TypedResults.Ok(await dienst.StatusAsync(ct)))
            .RequireAuthorization(Richtlinien.Lesen);
    }
}
