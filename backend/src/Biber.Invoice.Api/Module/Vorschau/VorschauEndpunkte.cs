using System.Security.Cryptography;
using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Biber.Invoice.Api.Module.Vorschau;

/// <summary>Seitenbilder eines Belegs als WebP (Karten, Aufteilen-Dialog, Logo-Ausschnitt).</summary>
public sealed class VorschauEndpunkte : IEndpunktModul
{
    private const int MinBreite = 200;
    private const int MaxBreite = 1600;

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Belege.Vorschau, async (
                Guid id, HttpContext http, BiberDbContext db, IVorschauDienst vorschau, CancellationToken ct, int seite = 1, int breite = 600) =>
            {
                var beleg = await db.Belege.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct);
                if (beleg is null || seite < 1)
                {
                    return Results.NotFound();
                }

                var webp = await vorschau.SeiteAsync(beleg, seite, Math.Clamp(breite, MinBreite, MaxBreite), ct);
                if (webp is null)
                {
                    return Results.NotFound();
                }

                var etag = $"\"{Convert.ToHexStringLower(SHA256.HashData(webp))[..32]}\"";
                http.Response.Headers.CacheControl = "private, max-age=86400";
                http.Response.Headers.ETag = etag;
                if (http.Request.Headers.IfNoneMatch.Contains(etag))
                {
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }

                return Results.Bytes(webp, "image/webp");
            })
            .RequireAuthorization(Richtlinien.Lesen);
    }
}
