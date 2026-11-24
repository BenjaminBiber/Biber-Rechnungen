using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Aufbewahrung;

namespace Biber.Invoice.Api.Module.Aufbewahrung;

/// <summary>Löschfreigabe (Admin): löschbare Jahre und Löschung der App-Daten eines Jahres.</summary>
public sealed class AufbewahrungEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Aufbewahrung.Loeschbar, LoeschbarAsync)
            .RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.Aufbewahrung.Loeschen, LoeschenAsync)
            .RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<IResult> LoeschbarAsync(AufbewahrungsDienst dienst, CancellationToken ct) =>
        TypedResults.Ok(await dienst.LoeschbarAsync(ct));

    private static async Task<IResult> LoeschenAsync(LoeschenDto anfrage, AufbewahrungsDienst dienst, CancellationToken ct)
    {
        if (anfrage.Bestaetigung != AufbewahrungsDienst.Bestaetigungswort)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["bestaetigung"] = [$"Zur Bestätigung „{AufbewahrungsDienst.Bestaetigungswort}“ eingeben."],
            });
        }

        var ergebnis = await dienst.LoeschenAsync(anfrage.Jahr, ct);
        return ergebnis is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Nichts zu löschen",
                detail: $"Für {anfrage.Jahr} gibt es keine Belege mit abgelaufener Aufbewahrungsfrist.")
            : TypedResults.Ok(ergebnis);
    }
}
