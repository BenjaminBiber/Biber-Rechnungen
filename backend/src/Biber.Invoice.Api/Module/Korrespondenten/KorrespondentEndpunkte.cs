using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Korrespondenten;

namespace Biber.Invoice.Api.Module.Korrespondenten;

/// <summary>Korrespondenten: Liste, Detail, Anlegen, Bearbeiten und Zusammenführen.</summary>
public sealed class KorrespondentEndpunkte : IEndpunktModul
{
    private static readonly string[] Typen =
        [KorrespondentDienst.TypLieferanten, KorrespondentDienst.TypKunden, KorrespondentDienst.TypAlle];

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Korrespondenten.Liste, ListeAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Korrespondenten.Liste, AnlegenAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Korrespondenten.Zusammenfuehren, ZusammenfuehrenAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapGet(ApiRouten.Korrespondenten.Einzeln, AbrufenAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPut(ApiRouten.Korrespondenten.Einzeln, AendernAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Korrespondenten.UstIdPruefen, UstIdPruefenAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten);
    }

    private static async Task<IResult> UstIdPruefenAsync(
        Guid id, IUstIdPruefDienst pruefung, IKorrespondentDienst dienst, TimeProvider zeit, CancellationToken ct) =>
        await pruefung.PruefenAsync(id, ct) && await dienst.AbrufenAsync(id, Jahr(zeit), ct) is { } detail
            ? TypedResults.Ok(detail)
            : NichtGefunden();

    private static async Task<IResult> ListeAsync(
        IKorrespondentDienst dienst, TimeProvider zeit, CancellationToken ct, string? suche = null, string? typ = null, int? jahr = null)
    {
        var gewaehlt = Typen.FirstOrDefault(t => string.Equals(t, typ, StringComparison.OrdinalIgnoreCase));
        if (typ is not null && gewaehlt is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["typ"] = ["Erlaubt sind „Lieferanten“, „Kunden“ und „Alle“."],
            });
        }

        var liste = await dienst.ListeAsync(suche, gewaehlt ?? KorrespondentDienst.TypLieferanten, jahr ?? Jahr(zeit), ct);
        return TypedResults.Ok(liste);
    }

    private static async Task<IResult> AbrufenAsync(
        Guid id, IKorrespondentDienst dienst, TimeProvider zeit, CancellationToken ct, int? jahr = null) =>
        await dienst.AbrufenAsync(id, jahr ?? Jahr(zeit), ct) is { } detail
            ? TypedResults.Ok(detail)
            : NichtGefunden();

    private static Task<IResult> AnlegenAsync(KorrespondentAendernDto eingabe, IKorrespondentDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () =>
        {
            var detail = await dienst.AnlegenAsync(eingabe, ct);
            return TypedResults.Created(ApiRouten.Korrespondenten.Einzeln.Replace("{id}", detail.Id.ToString(), StringComparison.Ordinal), detail);
        });

    private static Task<IResult> AendernAsync(Guid id, KorrespondentAendernDto eingabe, IKorrespondentDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () =>
            await dienst.AendernAsync(id, eingabe, ct) is { } detail ? TypedResults.Ok(detail) : NichtGefunden());

    private static Task<IResult> ZusammenfuehrenAsync(KorrespondentenZusammenfuehrenDto eingabe, IKorrespondentDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () =>
            await dienst.ZusammenfuehrenAsync(eingabe.BehaltenId, eingabe.QuellIds ?? [], ct) is { } detail
                ? TypedResults.Ok(detail)
                : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["behaltenId"] = ["Unbekannter Korrespondent."] }));

    private static async Task<IResult> AusfuehrenAsync(Func<Task<IResult>> aktion)
    {
        try
        {
            return await aktion();
        }
        catch (KorrespondentValidierungsAusnahme ex)
        {
            return TypedResults.ValidationProblem(ex.Fehler);
        }
        catch (KorrespondentKonfliktAusnahme ex)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Konflikt",
                detail: ex.Message,
                extensions: new Dictionary<string, object?> { ["korrespondentId"] = ex.VorhandenId });
        }
    }

    private static IResult NichtGefunden() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Korrespondent nicht gefunden.");

    private static int Jahr(TimeProvider zeit) => zeit.GetUtcNow().UtcDateTime.Year;
}
