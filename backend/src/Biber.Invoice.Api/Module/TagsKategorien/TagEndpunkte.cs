using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.TagsKategorien;

namespace Biber.Invoice.Api.Module.TagsKategorien;

/// <summary>Tags: Liste, Anlegen, Ändern, Löschen.</summary>
public sealed class TagEndpunkte : IEndpunktModul
{
    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Tags.Liste, async (TagVerwaltung tags, CancellationToken ct) =>
                TypedResults.Ok(await tags.ListeAsync(ct)))
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Tags.Liste, async (TagAendernDto dto, TagVerwaltung tags, CancellationToken ct) =>
                FehlerAntwort.Oder(await tags.AnlegenAsync(dto, ct), t => TypedResults.Created($"/api/tags/{t.Id}", t)))
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPut(ApiRouten.Tags.Einzeln, async (Guid id, TagAendernDto dto, TagVerwaltung tags, CancellationToken ct) =>
                FehlerAntwort.Oder(await tags.AendernAsync(id, dto, ct), TypedResults.Ok))
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapDelete(ApiRouten.Tags.Einzeln, async (Guid id, TagVerwaltung tags, CancellationToken ct) =>
                await tags.LoeschenAsync(id, ct) is { } fehler ? FehlerAntwort.Von(fehler) : TypedResults.NoContent())
            .RequireAuthorization(Richtlinien.Bearbeiten);
    }
}
