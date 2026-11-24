using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Biber.Invoice.Infrastructure.Module.Aufbewahrung;

/// <summary>
/// Löschsperre: Lehnt das Löschen eines <see cref="Beleg"/> oder seiner Originale und eingebetteten XML ab,
/// solange die Löschung nicht über <see cref="IAufbewahrungsFreigabe"/> angemeldet ist.
/// </summary>
public sealed class AufbewahrungsSchutz(IAufbewahrungsFreigabe freigabe) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } kontext)
        {
            Pruefen(kontext);
        }

        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } kontext)
        {
            Pruefen(kontext);
        }

        return ValueTask.FromResult(result);
    }

    private void Pruefen(DbContext kontext)
    {
        kontext.ChangeTracker.DetectChanges();
        foreach (var eintrag in kontext.ChangeTracker.Entries())
        {
            if (eintrag.State != EntityState.Deleted)
            {
                continue;
            }

            var belegId = eintrag.Entity switch
            {
                Beleg b => b.Id,
                BelegDatei { Rolle: DateiRolle.Original or DateiRolle.EingebettetesXml } d => d.BelegId,
                _ => (Guid?)null,
            };

            if (belegId is { } id && !freigabe.IstFreigegeben(id))
            {
                throw new InvalidOperationException(
                    $"Beleg {id} unterliegt der Aufbewahrungsfrist und darf nur über die Löschfreigabe gelöscht werden.");
            }
        }
    }
}
