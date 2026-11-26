using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Belege.Schreiben;

namespace Biber.Invoice.Api.Module.Belege;

/// <summary>
/// Belege schreiben: Korrektur von Kopf, Steuerzeilen und Positionen, Tags, Bestätigen, Dublette, erneutes Auslesen
/// und Massenaktionen. <c>aufteilen</c> und <c>logo-uebernehmen</c> liegen in eigenen Modulen.
/// </summary>
public sealed class BelegSchreibEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapPut(ApiRouten.Belege.Einzeln, AendernAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPut(ApiRouten.Belege.Positionen, PositionenAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Belege.Tags, TagHinzufuegenAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapDelete(ApiRouten.Belege.Tag, TagEntfernenAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Belege.Bestaetigen, BestaetigenAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Belege.ErneutAuslesen, ErneutAuslesenAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Belege.Dublette, DubletteAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Belege.Bezug, BezugAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPut(ApiRouten.Belege.Skonto, SkontoAsync).RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Belege.Massenaktion, MassenaktionAsync).RequireAuthorization(Richtlinien.Bearbeiten);
    }

    private static Task<IResult> AendernAsync(Guid id, BelegAendernDto aenderung, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => await dienst.AendernAsync(id, aenderung, ct) is { } detail ? TypedResults.Ok(detail) : NichtGefunden());

    private static Task<IResult> PositionenAsync(
        Guid id, PositionAendernDto[] positionen, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => await dienst.PositionenErsetzenAsync(id, positionen, ct) is { } detail ? TypedResults.Ok(detail) : NichtGefunden());

    private static Task<IResult> TagHinzufuegenAsync(Guid id, TagZuordnenDto tag, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => KeinInhalt(await dienst.TagHinzufuegenAsync(id, tag.TagId, ct)));

    private static Task<IResult> TagEntfernenAsync(Guid id, Guid tagId, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => KeinInhalt(await dienst.TagEntfernenAsync(id, tagId, ct)));

    private static Task<IResult> BestaetigenAsync(Guid id, BestaetigenDto? eingabe, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => KeinInhalt(await dienst.BestaetigenAsync(id, eingabe?.Begruendung, ct)));

    private static Task<IResult> ErneutAuslesenAsync(Guid id, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => await dienst.ErneutAuslesenAsync(id, ct) ? TypedResults.Accepted((string?)null) : NichtGefunden());

    private static Task<IResult> DubletteAsync(Guid id, DubletteSetzenDto eingabe, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => KeinInhalt(await dienst.DubletteSetzenAsync(id, eingabe.DuplikatVonId, ct)));

    private static Task<IResult> BezugAsync(Guid id, BezugSetzenDto eingabe, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => KeinInhalt(await dienst.BezugSetzenAsync(id, eingabe.BezugBelegId, ct)));

    private static Task<IResult> SkontoAsync(Guid id, SkontoSetzenDto eingabe, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => await dienst.SkontoSetzenAsync(id, eingabe, ct) is { } detail ? TypedResults.Ok(detail) : NichtGefunden());

    private static Task<IResult> MassenaktionAsync(MassenaktionDto aktion, IBelegBearbeitungsDienst dienst, CancellationToken ct) =>
        AusfuehrenAsync(async () => TypedResults.Ok(await dienst.MassenaktionAsync(aktion, ct)));

    private static async Task<IResult> AusfuehrenAsync(Func<Task<IResult>> aktion)
    {
        try
        {
            return await aktion();
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

    private static IResult KeinInhalt(bool gefunden) => gefunden ? TypedResults.NoContent() : NichtGefunden();

    private static IResult NichtGefunden() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Beleg nicht gefunden.");
}
