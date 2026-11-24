using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Integritaet;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Api.Module.Integritaet;

/// <summary>Läufe der Integritätsprüfung, manueller Start (Admin) und Stand des Datenbank-Backups.</summary>
public sealed class IntegritaetEndpunkte : IEndpunktModul
{
    public const int AnzahlLaeufe = 30;

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRouten.Integritaet.Laeufe, LaeufeAsync)
            .RequireAuthorization(Richtlinien.Lesen);
        app.MapPost(ApiRouten.Integritaet.Starten, StartenAsync)
            .RequireAuthorization(Richtlinien.Admin);
        app.MapGet(ApiRouten.SystemInfo.Backup, (BackupStatusLeser leser) => TypedResults.Ok(leser.Lesen()))
            .RequireAuthorization(Richtlinien.Lesen);
    }

    private static async Task<IResult> LaeufeAsync(BiberDbContext db, CancellationToken ct)
    {
        var laeufe = await db.IntegritaetsLaeufe.AsNoTracking()
            .OrderByDescending(l => l.GestartetAm)
            .Take(AnzahlLaeufe)
            .ToListAsync(ct);
        return TypedResults.Ok(laeufe.Select(l => new IntegritaetsLaufDto(
                l.Id,
                l.GestartetAm,
                l.BeendetAm is { } ende ? (int)Math.Round((ende - l.GestartetAm).TotalSeconds) : null,
                l.AnzahlDateien,
                l.AnzahlAbweichungen,
                [.. l.Abweichungen]))
            .ToArray());
    }

    private static async Task<IResult> StartenAsync(IntegritaetsPruefer pruefer, IIntegritaetsStarter starter, CancellationToken ct)
    {
        if (await pruefer.LaeuftAsync(ct))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Prüfung läuft bereits",
                detail: "Es läuft bereits eine Integritätsprüfung.");
        }

        starter.Starten();
        return TypedResults.Accepted((string?)null);
    }
}
