using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Belege.Lesen;
using Microsoft.Net.Http.Headers;

namespace Biber.Invoice.Api.Module.Belege;

/// <summary>
/// Belege lesen: Liste, Zähler, Detail, Nachbarn, ähnliche Belege, Dateien und Visualisierung, dazu die
/// gespeicherten Ansichten. <c>vorschau</c> und <c>historie</c> liegen in eigenen Modulen.
/// </summary>
public sealed class BelegLeseEndpunkte : IEndpunktModul
{
    /// <summary>Die Visualisierung ist fremdes HTML: kein Skript, keine Netzzugriffe, nur Inline-Styles und data:-Bilder.</summary>
    public const string VisualisierungCsp = "sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data:";

    public const int StandardAehnliche = 5;

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Belege.Liste, ListeAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Belege.Zaehler, ZaehlerAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Belege.Einzeln, AbrufenAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Belege.Nachbarn, NachbarnAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Belege.Aehnliche, AehnlicheAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Belege.Datei, DateiAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Belege.Visualisierung, VisualisierungAsync).RequireAuthorization(Richtlinien.Lesen);

        app.MapGet(ApiRouten.Ansichten.Liste, AnsichtenAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Ansichten.Liste, AnsichtSpeichernAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapDelete(ApiRouten.Ansichten.Einzeln, AnsichtLoeschenAsync).RequireAuthorization(Richtlinien.Lesen);
    }

    private static async Task<IResult> ListeAsync(HttpRequest anfrage, IBelegAbfrageDienst dienst, CancellationToken ct)
    {
        Dictionary<string, string[]> fehler = [];
        var filter = BelegFilterLeser.Lesen(anfrage.Query, fehler);
        return fehler.Count > 0
            ? TypedResults.ValidationProblem(fehler)
            : TypedResults.Ok(await dienst.SuchenAsync(filter, ct));
    }

    private static async Task<IResult> ZaehlerAsync(IBelegAbfrageDienst dienst, CancellationToken ct) =>
        TypedResults.Ok(await dienst.ZaehlerAsync(ct));

    private static async Task<IResult> AbrufenAsync(Guid id, IBelegAbfrageDienst dienst, CancellationToken ct) =>
        await dienst.AbrufenAsync(id, ct) is { } detail ? TypedResults.Ok(detail) : NichtGefunden();

    private static async Task<IResult> NachbarnAsync(Guid id, HttpRequest anfrage, IBelegAbfrageDienst dienst, CancellationToken ct)
    {
        Dictionary<string, string[]> fehler = [];
        var filter = BelegFilterLeser.Lesen(anfrage.Query, fehler);
        if (fehler.Count > 0)
        {
            return TypedResults.ValidationProblem(fehler);
        }

        try
        {
            return TypedResults.Ok(await dienst.NachbarnAsync(id, filter, ct));
        }
        catch (BelegNichtGefundenAusnahme)
        {
            return NichtGefunden();
        }
    }

    private static async Task<IResult> AehnlicheAsync(
        Guid id, IBelegAbfrageDienst dienst, CancellationToken ct, int anzahl = StandardAehnliche)
    {
        if (anzahl is < 1 or > BelegAbfrageDienst.MaxAehnliche)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["anzahl"] = [$"Die Anzahl muss zwischen 1 und {BelegAbfrageDienst.MaxAehnliche} liegen."],
            });
        }

        try
        {
            return TypedResults.Ok(await dienst.AehnlicheAsync(id, anzahl, ct));
        }
        catch (BelegNichtGefundenAusnahme)
        {
            return NichtGefunden();
        }
    }

    /// <summary>Streamt die Datei mit Range-Unterstützung; <c>inline=true</c> zeigt sie im Browser statt sie herunterzuladen.</summary>
    private static async Task<IResult> DateiAsync(
        Guid id, Guid dateiId, HttpResponse antwort, BelegDateiQuelle quelle, CancellationToken ct, bool inline = false)
    {
        if (await quelle.DateiAsync(id, dateiId, ct) is not { } datei)
        {
            return NichtGefunden();
        }

        if (!inline)
        {
            return TypedResults.PhysicalFile(datei.Pfad, datei.MimeType, datei.Dateiname, enableRangeProcessing: true);
        }

        var disposition = new ContentDispositionHeaderValue("inline");
        disposition.SetHttpFileName(datei.Dateiname);
        antwort.Headers.ContentDisposition = disposition.ToString();
        return TypedResults.PhysicalFile(datei.Pfad, datei.MimeType, enableRangeProcessing: true);
    }

    private static async Task<IResult> VisualisierungAsync(Guid id, HttpResponse antwort, BelegDateiQuelle quelle, CancellationToken ct)
    {
        if (await quelle.VisualisierungAsync(id, ct) is not { } datei)
        {
            return NichtGefunden();
        }

        antwort.Headers.ContentSecurityPolicy = VisualisierungCsp;
        antwort.Headers.XContentTypeOptions = "nosniff";
        return TypedResults.PhysicalFile(datei.Pfad, "text/html; charset=utf-8");
    }

    private static async Task<IResult> AnsichtenAsync(IAktuellerBenutzer benutzer, AnsichtenDienst dienst, CancellationToken ct) =>
        benutzer.Id is { } benutzerId ? TypedResults.Ok(await dienst.ListeAsync(benutzerId, ct)) : OhneBenutzer();

    private static async Task<IResult> AnsichtSpeichernAsync(
        GespeicherteAnsichtAnlegenDto? eingabe, IAktuellerBenutzer benutzer, AnsichtenDienst dienst, CancellationToken ct)
    {
        if (benutzer.Id is not { } benutzerId)
        {
            return OhneBenutzer();
        }

        var fehler = AnsichtenDienst.Pruefen(eingabe);
        if (fehler.Count > 0)
        {
            return TypedResults.ValidationProblem(fehler);
        }

        var ansicht = await dienst.SpeichernAsync(benutzerId, eingabe!, ct);
        return TypedResults.Created(
            ApiRouten.Ansichten.Einzeln.Replace("{id}", ansicht.Id.ToString(), StringComparison.Ordinal), ansicht);
    }

    private static async Task<IResult> AnsichtLoeschenAsync(Guid id, IAktuellerBenutzer benutzer, AnsichtenDienst dienst, CancellationToken ct)
    {
        if (benutzer.Id is not { } benutzerId)
        {
            return OhneBenutzer();
        }

        return await dienst.LoeschenAsync(benutzerId, id, ct) ? TypedResults.NoContent() : NichtGefunden();
    }

    private static IResult NichtGefunden() => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);

    private static IResult OhneBenutzer() => TypedResults.Problem(
        statusCode: StatusCodes.Status403Forbidden, title: "Kein Benutzer", detail: "Ansichten gehören zu einem angemeldeten Benutzer.");
}
