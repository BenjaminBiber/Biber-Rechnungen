using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.TagsKategorien;

namespace Biber.Invoice.Api.Module.TagsKategorien;

/// <summary>Kategorien: Liste mit Jahreskennzahlen, Anlegen, Ändern, Löschen.</summary>
public sealed class KategorieEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Kategorien.Liste, async (KategorieVerwaltung kategorien, CancellationToken ct, int? jahr) =>
                TypedResults.Ok(await kategorien.ListeAsync(jahr, ct)))
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Kategorien.Liste, async (KategorieAendernDto dto, KategorieVerwaltung kategorien, CancellationToken ct) =>
                FehlerAntwort.Oder(await kategorien.AnlegenAsync(dto, ct), k => TypedResults.Created($"/api/kategorien/{k.Id}", k)))
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPut(ApiRouten.Kategorien.Einzeln, async (Guid id, KategorieAendernDto dto, KategorieVerwaltung kategorien, CancellationToken ct) =>
                FehlerAntwort.Oder(await kategorien.AendernAsync(id, dto, ct), TypedResults.Ok))
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapDelete(ApiRouten.Kategorien.Einzeln, async (Guid id, KategorieVerwaltung kategorien, CancellationToken ct) =>
                FehlerAntwort.Oder(await kategorien.LoeschenAsync(id, ct), TypedResults.Ok))
            .RequireAuthorization(Richtlinien.Bearbeiten);
    }
}
