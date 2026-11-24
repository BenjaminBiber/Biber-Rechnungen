using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Biber.Invoice.Api.Module.Import;

/// <summary>Datei-Upload und Liste der Import-Vorgänge; die Arbeit macht <see cref="IImportDienst"/>.</summary>
public sealed class ImportEndpunkte : IEndpunktModul
{
    public const int MaxDateien = 20;
    public const long MaxDateiGroesse = 50L * 1024 * 1024;
    public const int MinTage = 1;
    public const int MaxTage = 90;

    // Obergrenze für den ganzen Request: alle Dateien plus Platz für die Multipart-Rahmen.
    private const long MaxAnfrageGroesse = MaxDateien * MaxDateiGroesse + 1024 * 1024;

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        // Antiforgery aus: Der Endpunkt hängt an der Cookie-/Token-Anmeldung, nicht an einem Formular der Anwendung.
        app.MapPost(ApiRouten.Import.Upload, UploadAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxAnfrageGroesse));
        app.MapGet(ApiRouten.Import.Vorgaenge, VorgaengeAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Import.Vorgang, VorgangAsync)
            .RequireAuthorization(Richtlinien.Lesen);
    }

    /// <summary>
    /// Nimmt jede Datei einzeln an. Abgelehnte Dateien (z. B. falsches Format) stehen mit Status „Abgelehnt“
    /// in der Antwort; die Anfrage scheitert deshalb nicht. Antwort 202 wie in <c>docs/api/openapi.json</c>,
    /// weil die Verarbeitung nach der Annahme asynchron weiterläuft.
    /// </summary>
    private static async Task<IResult> UploadAsync(
        HttpRequest anfrage, IImportDienst dienst, IAktuellerBenutzer benutzer, CancellationToken ct)
    {
        if (!anfrage.HasFormContentType)
        {
            return Ungueltig("dateien", "Die Anfrage muss multipart/form-data mit dem Feld „dateien“ sein.");
        }

        // Das Standardlimit für Multipart-Inhalte (128 MB) wäre kleiner als 20 Dateien à 50 MB.
        anfrage.HttpContext.Features.Set<IFormFeature>(
            new FormFeature(anfrage, new FormOptions { MultipartBodyLengthLimit = MaxAnfrageGroesse }));
        IFormCollection form;
        try
        {
            form = await anfrage.ReadFormAsync(ct);
        }
        catch (InvalidDataException)
        {
            return Ungueltig("dateien", "Das Formular ist ungültig oder unvollständig.");
        }

        var dateien = form.Files.GetFiles("dateien");
        if (dateien.Count == 0)
        {
            return Ungueltig("dateien", "Es wurde keine Datei hochgeladen.");
        }

        if (dateien.Count > MaxDateien)
        {
            return Ungueltig("dateien", $"Es sind höchstens {MaxDateien} Dateien je Upload erlaubt.");
        }

        if (dateien.FirstOrDefault(d => d.Length > MaxDateiGroesse) is { } zuGross)
        {
            return TypedResults.Problem(
                title: "Datei zu groß",
                detail: $"„{ImportDateiname.Bereinigen(zuGross.FileName)}“ ist größer als {MaxDateiGroesse / (1024 * 1024)} MB.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var herkunft = new ImportHerkunft(BenutzerId: benutzer.Id);
        var vorgaenge = new List<ImportVorgangDto>(dateien.Count);
        foreach (var datei in dateien)
        {
            await using var inhalt = datei.OpenReadStream();
            vorgaenge.Add(await dienst.AnnehmenAsync(
                inhalt, ImportDateiname.Bereinigen(datei.FileName), ImportQuelle.Upload, herkunft, ct));
        }

        return TypedResults.Json(vorgaenge, statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> VorgaengeAsync(
        IImportDienst dienst, CancellationToken ct, ImportQuelle? quelle = null, int tage = 7, int seite = 1)
    {
        Dictionary<string, string[]> fehler = [];
        if (tage is < MinTage or > MaxTage)
        {
            fehler["tage"] = [$"Die Anzahl der Tage muss zwischen {MinTage} und {MaxTage} liegen."];
        }

        if (seite < 1)
        {
            fehler["seite"] = ["Die Seite muss mindestens 1 sein."];
        }

        if (fehler.Count > 0)
        {
            return TypedResults.ValidationProblem(fehler);
        }

        SeiteDto<ImportVorgangDto> ergebnis = await dienst.VorgaengeAsync(quelle, tage, seite, ct);
        return TypedResults.Ok(ergebnis);
    }

    private static async Task<IResult> VorgangAsync(Guid id, IImportDienst dienst, CancellationToken ct) =>
        await dienst.VorgangAsync(id, ct) is { } vorgang
            ? TypedResults.Ok(vorgang)
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);

    private static IResult Ungueltig(string feld, string meldung) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [feld] = [meldung] });
}

/// <summary>Macht aus dem vom Client gelieferten Dateinamen einen unbedenklichen Namen.</summary>
public static class ImportDateiname
{
    public const int MaxLaenge = 200;
    private const string Ersatzname = "datei";

    /// <summary>Nur der Dateiname ohne Pfadanteile (<c>/</c> und <c>\</c>), ohne Steuerzeichen, höchstens 200 Zeichen.</summary>
    public static string Bereinigen(string? name)
    {
        name ??= string.Empty;
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        name = string.Concat(name.Where(c => !char.IsControl(c))).Trim();
        if (name.Length == 0 || name is "." or "..")
        {
            return Ersatzname;
        }

        if (name.Length <= MaxLaenge)
        {
            return name;
        }

        // Endung behalten, damit die Formatprüfung weiter greift.
        var endung = Path.GetExtension(name);
        if (endung.Length >= MaxLaenge)
        {
            endung = string.Empty;
        }

        return name[..(MaxLaenge - endung.Length)] + endung;
    }
}
