using System.Reflection;
using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Verfahrensdoku;

namespace Biber.Invoice.Api.Module.Verfahrensdoku;

/// <summary>Trägt die Software-Version der API (<c>AssemblyInformationalVersion</c>) bei.</summary>
public sealed class VerfahrensdokuApiModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        var assembly = typeof(VerfahrensdokuApiModul).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unbekannt";
        dienste.AddSingleton(new SoftwareVersion(version));
    }
}

/// <summary><c>GET /api/verfahrensdoku</c> und <c>GET /api/verfahrensdoku/pdf</c> (beide Lesen).</summary>
public sealed class VerfahrensdokuEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Verfahrensdoku.Info, InfoAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Verfahrensdoku.Pdf, PdfAsync)
            .RequireAuthorization(Richtlinien.Lesen);
    }

    private static async Task<IResult> InfoAsync(VerfahrensdokuDienst dienst, CancellationToken ct)
    {
        var dto = await dienst.LesenAsync(ct);
        return dto is null ? NichtGefunden() : Results.Ok(dto);
    }

    private static async Task<IResult> PdfAsync(VerfahrensdokuDienst dienst, HttpContext http, CancellationToken ct)
    {
        var pdf = await dienst.PdfAsync(ct);
        if (pdf is null)
        {
            return NichtGefunden();
        }

        // Results.File mit Dateiname würde „attachment“ setzen; gewünscht ist „inline“.
        http.Response.Headers.ContentDisposition = "inline; filename=\"Verfahrensdokumentation.pdf\"";
        return Results.Bytes(pdf, "application/pdf");
    }

    private static IResult NichtGefunden() =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Verfahrensdokumentation nicht gefunden");
}
