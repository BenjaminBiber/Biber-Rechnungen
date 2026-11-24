using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Logos;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Biber.Invoice.Api.Module.Logos;

/// <summary>Logo eines Korrespondenten lesen, hochladen, löschen, aus einem Beleg übernehmen und als Favicon laden.</summary>
public sealed class LogoEndpunkte : IEndpunktModul
{
    // Platz für die Multipart-Rahmen; zu große Dateien lehnt der Dienst mit 400 ab.
    private const long MaxAnfrageGroesse = LogoDienst.MaxUploadGroesse + 1024 * 1024;

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Korrespondenten.Logo, LesenAsync)
            .RequireAuthorization(Richtlinien.Lesen);

        // Antiforgery aus: Der Endpunkt hängt an der Cookie-/Token-Anmeldung, nicht an einem Formular der Anwendung.
        app.MapPut(ApiRouten.Korrespondenten.Logo, HochladenAsync)
            .RequireAuthorization(Richtlinien.Bearbeiten)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxAnfrageGroesse));

        app.MapDelete(ApiRouten.Korrespondenten.Logo, async Task<IResult> (Guid id, ILogoDienst dienst, CancellationToken ct) =>
                await dienst.LoeschenAsync(id, ct) ? TypedResults.NoContent() : NichtGefunden("Korrespondent nicht gefunden."))
            .RequireAuthorization(Richtlinien.Bearbeiten);

        app.MapPost(ApiRouten.Belege.LogoUebernehmen, (Guid id, LogoAusschnittDto ausschnitt, ILogoDienst dienst, CancellationToken ct) =>
                Ausfuehren(() => dienst.AusBelegAsync(id, ausschnitt, ct), "Beleg nicht gefunden."))
            .RequireAuthorization(Richtlinien.Bearbeiten);

        app.MapPost(ApiRouten.Korrespondenten.LogoFavicon, (Guid id, ILogoDienst dienst, CancellationToken ct) =>
                Ausfuehren(() => dienst.FaviconAsync(id, ct), "Korrespondent nicht gefunden."))
            .RequireAuthorization(Richtlinien.Bearbeiten);
    }

    private static async Task<IResult> LesenAsync(Guid id, HttpContext kontext, ILogoDienst dienst, CancellationToken ct)
    {
        var logo = await dienst.LesenAsync(id, ct);
        if (logo is null)
        {
            return NichtGefunden("Kein Logo vorhanden.");
        }

        kontext.Response.Headers.CacheControl = "private, max-age=3600";
        kontext.Response.Headers.ETag = logo.ETag;
        return kontext.Request.Headers.IfNoneMatch.Contains(logo.ETag)
            ? TypedResults.StatusCode(StatusCodes.Status304NotModified)
            : TypedResults.File(logo.Inhalt, "image/png");
    }

    private static async Task<IResult> HochladenAsync(Guid id, HttpRequest anfrage, ILogoDienst dienst, CancellationToken ct)
    {
        if (!anfrage.HasFormContentType)
        {
            return Ungueltig("datei", "Die Anfrage muss multipart/form-data mit dem Feld „datei“ sein.");
        }

        anfrage.HttpContext.Features.Set<IFormFeature>(
            new FormFeature(anfrage, new FormOptions { MultipartBodyLengthLimit = MaxAnfrageGroesse }));
        IFormCollection form;
        try
        {
            form = await anfrage.ReadFormAsync(ct);
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
        {
            return Ungueltig("datei", "Das Formular ist ungültig oder die Datei größer als 2 MB.");
        }

        if (form.Files.GetFile("datei") is not { } datei)
        {
            return Ungueltig("datei", "Es wurde keine Datei hochgeladen.");
        }

        if (datei.Length > LogoDienst.MaxUploadGroesse)
        {
            return Ungueltig("datei", "Das Logo darf höchstens 2 MB groß sein.");
        }

        using var puffer = new MemoryStream();
        await datei.CopyToAsync(puffer, ct);
        return await Ausfuehren(() => dienst.HochladenAsync(id, puffer.ToArray(), ct), "Korrespondent nicht gefunden.");
    }

    private static async Task<IResult> Ausfuehren<T>(Func<Task<T?>> aktion, string nichtGefunden)
        where T : class
    {
        try
        {
            return await aktion() is { } ergebnis ? TypedResults.Ok(ergebnis) : NichtGefunden(nichtGefunden);
        }
        catch (LogoAusnahme ex) when (ex.Status == LogoAusnahme.Ungueltig)
        {
            return Ungueltig(ex.Feld ?? "datei", ex.Message);
        }
        catch (LogoAusnahme ex)
        {
            return TypedResults.Problem(statusCode: ex.Status, title: ex.Message);
        }
    }

    private static IResult NichtGefunden(string titel) =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: titel);

    private static IResult Ungueltig(string feld, string meldung) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [feld] = [meldung] });
}
