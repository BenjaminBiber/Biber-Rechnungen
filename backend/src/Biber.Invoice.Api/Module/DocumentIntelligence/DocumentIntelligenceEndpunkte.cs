using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.DocumentIntelligence;

namespace Biber.Invoice.Api.Module.DocumentIntelligence;

/// <summary>Einstellungen → Document Intelligence: Verbrauch, Verbindungstest und Fortsetzen der Warteschlange.</summary>
public sealed class DocumentIntelligenceEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Einstellungen.DiVerbrauch, VerbrauchAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Einstellungen.DiVerbindungTesten, VerbindungTestenAsync)
            .RequireAuthorization(Richtlinien.Admin);
        app.MapPost(ApiRouten.Einstellungen.DiFortsetzen, FortsetzenAsync)
            .RequireAuthorization(Richtlinien.Admin);
    }

    private static async Task<IResult> VerbrauchAsync(DiVerbrauchsZaehler zaehler, CancellationToken ct) =>
        TypedResults.Ok(await zaehler.LesenAsync(ct));

    private static async Task<IResult> VerbindungTestenAsync(IDocumentIntelligenceClient client, CancellationToken ct)
    {
        if (!client.IstKonfiguriert)
        {
            return TypedResults.Ok(new AktionErgebnisDto(false, "Document Intelligence ist nicht konfiguriert (Endpunkt und Schlüssel in der .env)."));
        }

        return TypedResults.Ok(await client.VerbindungTestenAsync(ct)
            ? new AktionErgebnisDto(true, "Verbindung erfolgreich")
            : new AktionErgebnisDto(false, "Verbindung fehlgeschlagen"));
    }

    private static async Task<IResult> FortsetzenAsync(IImportDienst import, CancellationToken ct)
    {
        var anzahl = await import.PausierteFortsetzenAsync(ct);
        return TypedResults.Ok(new AktionErgebnisDto(true, anzahl == 1 ? "1 Beleg fortgesetzt" : $"{anzahl} Belege fortgesetzt"));
    }
}
