using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Auswertung;

namespace Biber.Invoice.Api.Module.Auswertung;

/// <summary><c>GET /api/dashboard</c> und <c>GET /api/system/status</c> (Lesen).</summary>
public sealed class AuswertungEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Auswertung.Dashboard, DashboardAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.SystemInfo.Status, SystemStatusAsync)
            .RequireAuthorization(Richtlinien.Lesen);
    }

    private static async Task<IResult> DashboardAsync(
        IAuswertungsDienst dienst,
        CancellationToken ct,
        string? zeitraum = null,
        int? jahr = null)
    {
        var gewaehlt = zeitraum is null
            ? AuswertungsDienst.ZeitraumMonat
            : AuswertungsDienst.Zeitraeume.FirstOrDefault(z => string.Equals(z, zeitraum, StringComparison.OrdinalIgnoreCase));
        if (gewaehlt is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["zeitraum"] = [$"Erlaubt sind {string.Join(", ", AuswertungsDienst.Zeitraeume)}."],
            });
        }

        if (jahr is < 1900 or > 9998)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["jahr"] = ["Das Jahr liegt außerhalb des gültigen Bereichs."],
            });
        }

        return TypedResults.Ok(await dienst.DashboardAsync(gewaehlt, jahr ?? 0, ct));
    }

    private static async Task<IResult> SystemStatusAsync(SystemStatusSammler sammler, CancellationToken ct) =>
        TypedResults.Ok(await sammler.LesenAsync(ct));
}
