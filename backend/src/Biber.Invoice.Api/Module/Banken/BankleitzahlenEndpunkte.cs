using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Banken;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Biber.Invoice.Api.Module.Banken;

/// <summary>Einstellungen → Prüfungen: Stand der Bankleitzahlen, Abruf bei der Bundesbank und Hochladen der Datei (Admin).</summary>
public sealed class BankleitzahlenEndpunkte : IEndpunktModul
{
    // Platz für die Multipart-Rahmen; zu große Dateien lehnt der Endpunkt mit 400 ab.
    private const long MaxAnfrageGroesse = BankleitzahlenDienst.MaxUploadGroesse + 1024 * 1024;

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Bankleitzahlen.Stand, async (BankleitzahlenDienst dienst, CancellationToken ct) =>
                TypedResults.Ok(await dienst.StandAsync(ct)))
            .RequireAuthorization(Richtlinien.Admin);

        app.MapPost(ApiRouten.Bankleitzahlen.Abrufen, async (BankleitzahlenDienst dienst, CancellationToken ct) =>
                TypedResults.Ok(await dienst.AbrufenAsync(erzwingen: true, ct)))
            .RequireAuthorization(Richtlinien.Admin);

        // Antiforgery aus: Der Endpunkt hängt an der Cookie-/Token-Anmeldung, nicht an einem Formular der Anwendung.
        app.MapPut(ApiRouten.Bankleitzahlen.Stand, HochladenAsync)
            .RequireAuthorization(Richtlinien.Admin)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxAnfrageGroesse));
    }

    private static async Task<IResult> HochladenAsync(HttpRequest anfrage, BankleitzahlenDienst dienst, CancellationToken ct)
    {
        if (!anfrage.HasFormContentType)
        {
            return Ungueltig("Die Anfrage muss multipart/form-data mit dem Feld „datei“ sein.");
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
            return Ungueltig("Das Formular ist ungültig oder die Datei größer als 10 MB.");
        }

        if (form.Files.GetFile("datei") is not { } datei)
        {
            return Ungueltig("Es wurde keine Datei hochgeladen.");
        }

        if (datei.Length > BankleitzahlenDienst.MaxUploadGroesse)
        {
            return Ungueltig("Die Datei darf höchstens 10 MB groß sein.");
        }

        using var puffer = new MemoryStream();
        await datei.CopyToAsync(puffer, ct);
        try
        {
            return TypedResults.Ok(await dienst.HochladenAsync(puffer.ToArray(), datei.FileName, ct));
        }
        catch (BankleitzahlenAusnahme ex)
        {
            return Ungueltig(ex.Message);
        }
    }

    private static IResult Ungueltig(string meldung) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["datei"] = [meldung] });
}
