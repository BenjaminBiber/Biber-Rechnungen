using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Export;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Export;
using Microsoft.AspNetCore.StaticFiles;

namespace Biber.Invoice.Api.Module.Export;

/// <summary>Vorschau und Erzeugung von Exporten (Bearbeiten), Liste und Download der Läufe (Lesen).</summary>
public sealed class ExportEndpunkte : IEndpunktModul
{
    private static readonly FileExtensionContentTypeProvider Inhaltstypen = new();

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRouten.Export.Vorschau, VorschauAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPost(ApiRouten.Export.Erzeugen, ErzeugenAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapGet(ApiRouten.Export.Laeufe, LaeufeAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Export.LaufDatei, DateiAsync)
            .RequireAuthorization(Richtlinien.Lesen);
    }

    private static async Task<IResult> VorschauAsync(ExportAnfrageDto anfrage, ExportDienst dienst, CancellationToken ct) =>
        ExportDienst.Pruefen(anfrage, bestandteilNoetig: false) is { } fehler
            ? TypedResults.ValidationProblem(fehler)
            : TypedResults.Ok(await dienst.VorschauAsync(anfrage, ct));

    private static async Task<IResult> ErzeugenAsync(ExportAnfrageDto anfrage, ExportDienst dienst, CancellationToken ct)
    {
        if (ExportDienst.Pruefen(anfrage, bestandteilNoetig: true) is { } fehler)
        {
            return TypedResults.ValidationProblem(fehler);
        }

        var ergebnis = await dienst.ErzeugenAsync(anfrage, ct);
        if (ergebnis.NichtsZuExportieren)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["vorsteuer"] = ["Bei Kleinunternehmerregelung entfällt die Vorsteuer; einen weiteren Bestandteil wählen."],
            });
        }

        if (ergebnis.Lauf is not { } lauf)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Originale verändert",
                detail: "Der Hash dieser Originale weicht vom gespeicherten Wert ab; der Export wurde nicht erzeugt.",
                extensions: new Dictionary<string, object?> { ["pfade"] = ergebnis.Abweichungen });
        }

        return TypedResults.Created(lauf.DownloadUrl, lauf);
    }

    private static async Task<IResult> LaeufeAsync(ExportDienst dienst, CancellationToken ct) =>
        TypedResults.Ok(await dienst.LaeufeAsync(ct));

    private static async Task<IResult> DateiAsync(Guid id, ExportDienst dienst, CancellationToken ct)
    {
        var datei = await dienst.DateiAsync(id, ct);
        if (datei is null)
        {
            return TypedResults.NotFound();
        }

        if (datei.Pfad is not { } pfad)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status410Gone, title: "Datei nicht mehr vorhanden",
                detail: $"Die Exportdatei „{datei.Dateiname}“ liegt nicht mehr auf der Platte.");
        }

        var typ = Inhaltstypen.TryGetContentType(datei.Dateiname, out var bekannt) ? bekannt : "application/octet-stream";
        return TypedResults.PhysicalFile(pfad, typ, datei.Dateiname);
    }
}
