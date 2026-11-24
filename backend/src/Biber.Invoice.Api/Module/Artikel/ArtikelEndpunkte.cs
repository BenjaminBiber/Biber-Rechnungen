using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Artikel;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Artikelpreise;

// Namespace bewusst nicht „Module.Artikel“: er würde in allen Nachbarmodulen den Entitätstyp Artikel verdecken.
namespace Biber.Invoice.Api.Module.Artikelpreise;

/// <summary>Artikel und Stammartikel: Listen und Preisdetails (Lesen), Stammartikel verwalten (Bearbeiten).</summary>
public sealed class ArtikelEndpunkte : IEndpunktModul
{
    private const int MaxSeitengroesse = 200;

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Artikel.Liste, ListeAsync).RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Artikel.Einzeln, async (Guid id, int? jahr, IArtikelDienst dienst, TimeProvider zeit, CancellationToken ct) =>
                await dienst.ArtikelDetailAsync(id, jahr ?? zeit.GetUtcNow().Year, ct) is { } detail
                    ? Results.Ok(detail)
                    : NichtGefunden("Artikel"))
            .RequireAuthorization(Richtlinien.Lesen);

        app.MapGet(ApiRouten.Stammartikel.Liste, async (IArtikelDienst dienst, CancellationToken ct) =>
                Results.Ok(await dienst.StammartikelListeAsync(ct)))
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapGet(ApiRouten.Stammartikel.Einzeln, async (Guid id, int? jahr, IArtikelDienst dienst, TimeProvider zeit, CancellationToken ct) =>
                await dienst.StammartikelDetailAsync(id, jahr ?? zeit.GetUtcNow().Year, ct) is { } detail
                    ? Results.Ok(detail)
                    : NichtGefunden("Stammartikel"))
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Stammartikel.Liste, (StammartikelAendernDto dto, IArtikelDienst dienst, CancellationToken ct) =>
                SpeichernAsync(null, dto, dienst, ct))
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapPut(ApiRouten.Stammartikel.Einzeln, (Guid id, StammartikelAendernDto dto, IArtikelDienst dienst, CancellationToken ct) =>
                SpeichernAsync(id, dto, dienst, ct))
            .RequireAuthorization(Richtlinien.Bearbeiten);
        app.MapDelete(ApiRouten.Stammartikel.Artikel, async (Guid id, Guid artikelId, IArtikelDienst dienst, CancellationToken ct) =>
                await dienst.ArtikelHerausloesenAsync(id, artikelId, ct)
                    ? Results.NoContent()
                    : NichtGefunden("Artikel im Stammartikel"))
            .RequireAuthorization(Richtlinien.Bearbeiten);
    }

    private static async Task<IResult> ListeAsync(
        string? suche,
        Guid? korrespondentId,
        bool? ohneStammartikel,
        PreisaenderungFilter? preisaenderung,
        int? seite,
        int? seitengroesse,
        IArtikelDienst dienst,
        CancellationToken ct)
    {
        var filter = new ArtikelFilterDto
        {
            Suche = suche,
            KorrespondentId = korrespondentId,
            OhneStammartikel = ohneStammartikel ?? false,
            Preisaenderung = preisaenderung,
            Seite = seite ?? 1,
            Seitengroesse = seitengroesse ?? 50,
        };

        var fehler = new Dictionary<string, string[]>();
        if (filter.Seite < 1)
        {
            fehler["seite"] = ["Die Seite beginnt bei 1."];
        }

        if (filter.Seitengroesse is < 1 or > MaxSeitengroesse)
        {
            fehler["seitengroesse"] = [$"Erlaubt ist 1 bis {MaxSeitengroesse}."];
        }

        return fehler.Count > 0
            ? Results.ValidationProblem(fehler)
            : Results.Ok(await dienst.SuchenAsync(filter, ct));
    }

    private static async Task<IResult> SpeichernAsync(Guid? id, StammartikelAendernDto dto, IArtikelDienst dienst, CancellationToken ct)
    {
        var fehler = Pruefen(dto, anlegen: id is null);
        if (fehler.Count > 0)
        {
            return Results.ValidationProblem(fehler);
        }

        try
        {
            var ergebnis = await dienst.StammartikelSpeichernAsync(id, dto, ct);
            if (ergebnis is null)
            {
                return NichtGefunden("Stammartikel");
            }

            return id is null
                ? Results.Created(ApiRouten.Stammartikel.Einzeln.Replace("{id}", ergebnis.Id.ToString()), ergebnis)
                : Results.Ok(ergebnis);
        }
        catch (StammartikelUngueltigException e)
        {
            return Results.ValidationProblem(e.Fehler.ToDictionary());
        }
        catch (StammartikelKonfliktException e)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Artikel bereits zugeordnet", detail: e.Message);
        }
    }

    private static Dictionary<string, string[]> Pruefen(StammartikelAendernDto dto, bool anlegen)
    {
        var fehler = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            fehler["name"] = ["Der Name ist Pflicht."];
        }

        if (string.IsNullOrWhiteSpace(dto.Basiseinheit))
        {
            fehler["basiseinheit"] = ["Die Basiseinheit ist Pflicht."];
        }

        if (dto.GrundpreisMenge <= 0)
        {
            fehler["grundpreisMenge"] = ["Die Bezugsmenge muss größer als 0 sein."];
        }

        var artikel = dto.Artikel ?? [];
        if (anlegen && artikel.Length == 0)
        {
            fehler["artikel"] = ["Mindestens ein Artikel ist nötig."];
        }

        if (artikel.Any(a => a.UmrechnungFaktor is <= 0))
        {
            fehler["umrechnungFaktor"] = ["Der Umrechnungsfaktor muss größer als 0 sein."];
        }

        if (artikel.GroupBy(a => a.ArtikelId).Any(g => g.Count() > 1))
        {
            fehler["artikel"] = ["Jeder Artikel darf nur einmal vorkommen."];
        }

        return fehler;
    }

    private static IResult NichtGefunden(string was) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"{was} nicht gefunden.");
}
