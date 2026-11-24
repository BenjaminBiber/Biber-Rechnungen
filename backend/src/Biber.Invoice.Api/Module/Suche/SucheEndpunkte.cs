using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;

namespace Biber.Invoice.Api.Module.Suche;

/// <summary><c>GET /api/suche?q=</c>: Treffer für die Befehlspalette in einer Antwort.</summary>
public sealed class SucheEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Suche.Global, async (string? q, ISuchDienst dienst, CancellationToken ct) =>
            {
                if (q is not null && q.Trim().Length > ISuchDienst.MaxLaenge)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["q"] = [$"Der Suchbegriff darf höchstens {ISuchDienst.MaxLaenge} Zeichen lang sein."],
                    });
                }

                return Results.Ok(await dienst.SuchenAsync(q, ct));
            })
            .RequireAuthorization(Richtlinien.Lesen);
    }
}
