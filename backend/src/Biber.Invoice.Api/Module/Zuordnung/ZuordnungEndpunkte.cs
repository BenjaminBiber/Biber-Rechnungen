using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.TagsKategorien;

namespace Biber.Invoice.Api.Module.Zuordnung;

/// <summary>Matching-Regeln testen und auf den Bestand anwenden.</summary>
public sealed class ZuordnungEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRouten.Matching.Testen, async (MatchingTestDto dto, IZuordnungsDienst zuordnung, CancellationToken ct) =>
            {
                if (MatchingPruefung.Pruefe(dto.Matching) is { } fehler)
                {
                    return Ungueltig(fehler.Feld ?? "matching", fehler.Meldung);
                }

                try
                {
                    return TypedResults.Ok(await zuordnung.TestenAsync(dto, ct));
                }
                catch (ArgumentException e)
                {
                    return Ungueltig("ziel", e.Message);
                }
            })
            .RequireAuthorization(Richtlinien.Bearbeiten);

        app.MapPost(ApiRouten.Matching.Anwenden, async (MatchingAnwendenDto dto, IZuordnungsDienst zuordnung, CancellationToken ct) =>
            {
                try
                {
                    return TypedResults.Ok(new AnzahlGeaendertDto(await zuordnung.AnwendenAsync(dto.Ziel, dto.ZielId, ct)));
                }
                catch (ArgumentException e)
                {
                    return Ungueltig("ziel", e.Message);
                }
                catch (KeyNotFoundException e)
                {
                    return TypedResults.Problem(detail: e.Message, statusCode: StatusCodes.Status404NotFound);
                }
            })
            .RequireAuthorization(Richtlinien.Bearbeiten);
    }

    private static IResult Ungueltig(string feld, string meldung) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [feld] = [meldung] });
}
