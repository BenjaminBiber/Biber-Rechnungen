using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.AutoMatching;

public sealed class AutoMatchingDienst(BiberDbContext db) : IAutoMatchingDienst
{
    public async Task<AutoMatchingStatusDto[]> StatusAsync(CancellationToken ct)
    {
        var modelle = await db.MatchingModelle.AsNoTracking()
            .OrderBy(m => m.Ziel)
            .ThenByDescending(m => m.TrainiertAm)
            .Select(m => new { m.Ziel, m.TrainiertAm, m.AnzahlBeispiele, m.Guete })
            .ToListAsync(ct);

        return [.. modelle
            .DistinctBy(m => m.Ziel)
            .Select(m => new AutoMatchingStatusDto(
                m.Ziel,
                m.TrainiertAm,
                m.AnzahlBeispiele,
                m.Guete is { } guete ? Math.Round((decimal)guete, 4) : null))];
    }
}
