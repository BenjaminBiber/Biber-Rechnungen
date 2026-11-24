using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Benachrichtigungen;

/// <summary>
/// Liest und markiert Benachrichtigungen für den angemeldeten Benutzer. Sichtbar sind Einträge, die ab
/// dem Anlegen des Benutzers entstanden sind; Einträge mit <see cref="Benachrichtigung.NurAdmins"/>
/// nur für Admins. „Gelesen“ gilt je Benutzer.
/// </summary>
public sealed class BenachrichtigungsAbfrage(BiberDbContext db, IAktuellerBenutzer benutzer, TimeProvider zeit)
{
    public const int Seitengroesse = 20;

    public async Task<SeiteDto<BenachrichtigungDto>> ListeAsync(bool nurUngelesen, int seite, CancellationToken ct)
    {
        if (await SichtbareAsync(ct) is not var (benutzerId, sichtbar))
        {
            return new SeiteDto<BenachrichtigungDto>([], 0, seite, Seitengroesse);
        }

        if (nurUngelesen)
        {
            sichtbar = sichtbar.Where(b => !b.Gelesen.Any(g => g.BenutzerId == benutzerId));
        }

        var gesamt = await sichtbar.CountAsync(ct);
        var eintraege = await sichtbar
            .OrderByDescending(b => b.ErstelltAm)
            .ThenByDescending(b => b.Id)
            .Skip((seite - 1) * Seitengroesse)
            .Take(Seitengroesse)
            .Select(b => new BenachrichtigungDto(
                b.Id,
                b.Art,
                b.Schweregrad,
                b.Titel,
                b.Text,
                b.Link,
                b.ErstelltAm,
                b.Gelesen.Any(g => g.BenutzerId == benutzerId)))
            .ToListAsync(ct);
        return new SeiteDto<BenachrichtigungDto>(eintraege, gesamt, seite, Seitengroesse);
    }

    public async Task<BenachrichtigungZaehlerDto> ZaehlerAsync(CancellationToken ct)
    {
        if (await SichtbareAsync(ct) is not var (benutzerId, sichtbar))
        {
            return new BenachrichtigungZaehlerDto(0);
        }

        var ungelesen = await sichtbar.CountAsync(b => !b.Gelesen.Any(g => g.BenutzerId == benutzerId), ct);
        return new BenachrichtigungZaehlerDto(ungelesen);
    }

    /// <summary>Markiert eine Benachrichtigung als gelesen (idempotent); <c>false</c>, wenn sie für den Benutzer nicht existiert.</summary>
    public async Task<bool> MarkiereGelesenAsync(Guid id, CancellationToken ct)
    {
        if (await SichtbareAsync(ct) is not var (benutzerId, sichtbar) || !await sichtbar.AnyAsync(b => b.Id == id, ct))
        {
            return false;
        }

        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO benachrichtigung_gelesen (benachrichtigung_id, benutzer_id, gelesen_am)
            VALUES ({id}, {benutzerId}, {zeit.GetUtcNow()})
            ON CONFLICT (benachrichtigung_id, benutzer_id) DO NOTHING
            """,
            ct);
        return true;
    }

    public async Task MarkiereAlleGelesenAsync(CancellationToken ct)
    {
        if (await SichtbareAsync(ct) is not var (benutzerId, sichtbar))
        {
            return;
        }

        var ungelesen = await sichtbar
            .Where(b => !b.Gelesen.Any(g => g.BenutzerId == benutzerId))
            .Select(b => b.Id)
            .ToListAsync(ct);
        var jetzt = zeit.GetUtcNow();
        foreach (var id in ungelesen)
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO benachrichtigung_gelesen (benachrichtigung_id, benutzer_id, gelesen_am)
                VALUES ({id}, {benutzerId}, {jetzt})
                ON CONFLICT (benachrichtigung_id, benutzer_id) DO NOTHING
                """,
                ct);
        }
    }

    /// <summary>Benutzer-ID und die für ihn sichtbaren Einträge; <c>null</c>, wenn es den Benutzer nicht gibt.</summary>
    private async Task<(Guid BenutzerId, IQueryable<Benachrichtigung> Sichtbar)?> SichtbareAsync(CancellationToken ct)
    {
        if (benutzer.Id is not { } benutzerId)
        {
            return null;
        }

        var angelegtAm = await db.Users
            .Where(u => u.Id == benutzerId)
            .Select(u => (DateTimeOffset?)u.ErstelltAm)
            .SingleOrDefaultAsync(ct);
        if (angelegtAm is not { } seit)
        {
            return null;
        }

        var istAdmin = benutzer.HatRolle(Rolle.Admin);
        return (benutzerId, db.Benachrichtigungen
            .AsNoTracking()
            .Where(b => b.ErstelltAm >= seit && (istAdmin || !b.NurAdmins)));
    }
}
