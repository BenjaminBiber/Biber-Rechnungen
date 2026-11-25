using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Positionen;

/// <summary>
/// Ein Artikel existiert nur über seine Positionen. Verliert er die letzte (Position umbenannt, Beleg einem anderen
/// Korrespondenten zugeordnet, neu ausgelesen oder gelöscht), wird er entfernt; ein dadurch leerer Stammartikel ebenso.
/// </summary>
public static class VerwaisteArtikel
{
    /// <summary>Artikel-Ids der Positionen; vor der Änderung erfassen und danach an <see cref="EntfernenAsync"/> geben.</summary>
    public static List<Guid> Kandidaten(IEnumerable<Domain.Entitaeten.Position> positionen) =>
        [.. positionen.Where(p => p.ArtikelId is not null).Select(p => p.ArtikelId!.Value).Distinct()];

    /// <summary>Löscht von den <paramref name="kandidaten"/> die Artikel ohne Position (nach dem Speichern aufrufen).</summary>
    /// <returns>Anzahl gelöschter Artikel.</returns>
    public static async Task<int> EntfernenAsync(BiberDbContext db, IReadOnlyCollection<Guid> kandidaten, CancellationToken ct)
    {
        if (kandidaten.Count == 0)
        {
            return 0;
        }

        var verwaist = await db.Artikel
            .Where(a => kandidaten.Contains(a.Id) && !db.Positionen.Any(p => p.ArtikelId == a.Id))
            .Select(a => new { a.Id, a.StammartikelId })
            .ToListAsync(ct);
        if (verwaist.Count == 0)
        {
            return 0;
        }

        var ids = verwaist.Select(a => a.Id).ToList();
        var stammIds = verwaist.Where(a => a.StammartikelId is not null).Select(a => a.StammartikelId!.Value).Distinct().ToList();

        // Ein frisch zugeordneter Artikel hat seit der Abfrage oben eventuell wieder eine Position bekommen.
        var geloescht = await db.Artikel
            .Where(a => ids.Contains(a.Id) && !db.Positionen.Any(p => p.ArtikelId == a.Id))
            .ExecuteDeleteAsync(ct);
        if (stammIds.Count > 0)
        {
            await db.Stammartikel
                .Where(s => stammIds.Contains(s.Id) && !db.Artikel.Any(a => a.StammartikelId == s.Id))
                .ExecuteDeleteAsync(ct);
        }

        return geloescht;
    }
}
