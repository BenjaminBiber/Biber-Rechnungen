using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.DocumentIntelligence;

/// <summary>
/// Zählt die von Document Intelligence verarbeiteten Seiten je Monat (<c>di_verbrauch</c>). Der Monat ist der
/// Monatserste in der Zeitzone aus <see cref="AllgemeinEinstellungen.Zeitzone"/>.
/// </summary>
public sealed class DiVerbrauchsZaehler(BiberDbContext db, IEinstellungen einstellungen, TimeProvider zeit)
{
    /// <summary>Erster Tag des aktuellen Monats in der eingestellten Zeitzone.</summary>
    public async Task<DateOnly> MonatAsync(CancellationToken ct)
    {
        var allgemein = await einstellungen.LesenAsync<AllgemeinEinstellungen>(ct);
        var jetzt = TimeZoneInfo.ConvertTime(zeit.GetUtcNow(), Zeitzone(allgemein.Zeitzone));
        return new DateOnly(jetzt.Year, jetzt.Month, 1);
    }

    public async Task<DiVerbrauchDto> LesenAsync(CancellationToken ct)
    {
        var monat = await MonatAsync(ct);
        var limit = (await einstellungen.LesenAsync<DiEinstellungen>(ct)).Monatslimit;
        var seiten = await SeitenAsync(monat, ct);
        var wartend = await db.ImportVorgaenge.CountAsync(v => v.Status == ImportStatus.Pausiert, ct);
        return new DiVerbrauchDto(monat.ToString("yyyy-MM"), seiten, limit, Prozent(seiten, limit), wartend > 0 || seiten >= limit, wartend);
    }

    /// <summary>Passen <paramref name="seiten"/> weitere Seiten noch in das Monatslimit?</summary>
    public async Task<bool> PasstNochAsync(int seiten, CancellationToken ct)
    {
        var monat = await MonatAsync(ct);
        var limit = (await einstellungen.LesenAsync<DiEinstellungen>(ct)).Monatslimit;
        return await SeitenAsync(monat, ct) + seiten <= limit;
    }

    /// <summary>Erhöht den Verbrauch des aktuellen Monats atomar (auch bei parallelen Jobs).</summary>
    /// <returns>Verbrauch des Monats nach der Erhöhung.</returns>
    public async Task<int> ErhoehenAsync(int seiten, CancellationToken ct)
    {
        var monat = await MonatAsync(ct);
        var ergebnis = await db.Database
            .SqlQuery<int>($"""
                INSERT INTO di_verbrauch (monat, seiten) VALUES ({monat}, {seiten})
                ON CONFLICT (monat) DO UPDATE SET seiten = di_verbrauch.seiten + EXCLUDED.seiten
                RETURNING seiten AS "Value"
                """)
            .ToListAsync(ct);
        return ergebnis[0];
    }

    /// <summary>Anteil in ganzen Prozent (abgerundet); ein Limit von 0 gilt als ausgeschöpft.</summary>
    public static int Prozent(int seiten, int limit) => limit <= 0 ? 100 : (int)(seiten * 100L / limit);

    private async Task<int> SeitenAsync(DateOnly monat, CancellationToken ct) =>
        await db.DiVerbraeuche.AsNoTracking().Where(d => d.Monat == monat).Select(d => d.Seiten).FirstOrDefaultAsync(ct);

    private static TimeZoneInfo Zeitzone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
