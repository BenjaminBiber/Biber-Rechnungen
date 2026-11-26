using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.NeuAuslesen;

/// <summary>
/// „Alle Belege erneut auslesen“: startet einen <see cref="NeuAuslesenLauf"/> (immer nur einen zur Zeit), liefert
/// seinen Stand samt Restzeit und bricht ihn auf Wunsch ab. Die Arbeit selbst macht der <see cref="NeuAuslesenJob"/>.
/// </summary>
public sealed class NeuAuslesenDienst(
    BiberDbContext db,
    INeuAuslesenStarter starter,
    TimeProvider zeit,
    ILogger<NeuAuslesenDienst> log)
{
    /// <summary>Ohne Fortschritt über diese Zeit gilt ein offener Lauf als hängen geblieben (z. B. nach einem Absturz).</summary>
    public static readonly TimeSpan Haengezeit = TimeSpan.FromMinutes(30);

    public const string GrundUnterbrochen = "Unterbrochen";
    public const string GrundAbgebrochen = "Von Hand abgebrochen";

    private static readonly SemaphoreSlim Sperre = new(1, 1);

    /// <summary>Belege, die ein Lauf mit Stichtag <paramref name="stichtag"/> neu ausliest: nicht gelöscht, mit Original.</summary>
    public static IQueryable<Beleg> Belege(BiberDbContext db, DateTimeOffset stichtag) =>
        db.Belege.Where(b => b.ErstelltAm <= stichtag && b.Dateien.Any(d => d.Rolle == DateiRolle.Original));

    public static bool Laeuft(NeuAuslesenLauf lauf, DateTimeOffset jetzt) =>
        lauf.BeendetAm is null && lauf.AktualisiertAm > jetzt - Haengezeit;

    public async Task<NeuAuslesenStatusDto> StatusAsync(CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var anzahl = await Belege(db, jetzt).CountAsync(ct);
        var lauf = await db.NeuAuslesenLaeufe.AsNoTracking().OrderByDescending(l => l.GestartetAm).FirstOrDefaultAsync(ct);
        return new NeuAuslesenStatusDto(anzahl, lauf is null ? null : AlsDto(lauf, jetzt));
    }

    /// <summary>Legt einen neuen Lauf an und reiht ihn ein; <c>null</c>, wenn schon einer läuft.</summary>
    public async Task<NeuAuslesenStatusDto?> StartenAsync(CancellationToken ct)
    {
        await Sperre.WaitAsync(ct);
        try
        {
            var jetzt = zeit.GetUtcNow();
            var offen = await db.NeuAuslesenLaeufe.Where(l => l.BeendetAm == null).ToListAsync(ct);
            if (offen.Any(l => Laeuft(l, jetzt)))
            {
                return null;
            }

            foreach (var haengend in offen)
            {
                haengend.BeendetAm = jetzt;
                haengend.Abbruchgrund ??= GrundUnterbrochen;
            }

            var lauf = new NeuAuslesenLauf
            {
                Id = Guid.NewGuid(),
                GestartetAm = jetzt,
                AktualisiertAm = jetzt,
                Gesamt = await Belege(db, jetzt).CountAsync(ct),
            };
            if (lauf.Gesamt == 0)
            {
                lauf.BeendetAm = jetzt;
            }

            db.NeuAuslesenLaeufe.Add(lauf);
            await db.SaveChangesAsync(ct);
            if (lauf.BeendetAm is null)
            {
                starter.Starten(lauf.Id);
            }

            log.LogInformation("Erneutes Auslesen aller Belege gestartet: {Anzahl} Belege (Lauf {Lauf})", lauf.Gesamt, lauf.Id);
        }
        finally
        {
            Sperre.Release();
        }

        return await StatusAsync(ct);
    }

    /// <summary>
    /// Bittet den laufenden Job, nach dem aktuellen Beleg aufzuhören; einen hängen gebliebenen Lauf schließt er
    /// sofort. <c>null</c>, wenn kein Lauf offen ist.
    /// </summary>
    public async Task<NeuAuslesenStatusDto?> AbbrechenAsync(CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var lauf = await db.NeuAuslesenLaeufe.Where(l => l.BeendetAm == null).OrderByDescending(l => l.GestartetAm).FirstOrDefaultAsync(ct);
        if (lauf is null)
        {
            return null;
        }

        lauf.AbbruchAngefordert = true;
        lauf.Abbruchgrund ??= GrundAbgebrochen;
        if (!Laeuft(lauf, jetzt))
        {
            lauf.BeendetAm = jetzt;
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("Erneutes Auslesen abgebrochen nach {Verarbeitet} von {Gesamt} Belegen (Lauf {Lauf})", lauf.Verarbeitet, lauf.Gesamt, lauf.Id);
        return await StatusAsync(ct);
    }

    public static NeuAuslesenLaufDto AlsDto(NeuAuslesenLauf lauf, DateTimeOffset jetzt)
    {
        var laeuft = Laeuft(lauf, jetzt);
        var ende = lauf.BeendetAm ?? (laeuft ? jetzt : lauf.AktualisiertAm);
        var rest = Math.Max(0, lauf.Gesamt - lauf.Verarbeitet);
        int? restSekunden = !laeuft ? null
            : rest == 0 ? 0
            : lauf.Verarbeitet == 0 ? null
            : (int)Math.Ceiling((lauf.AktualisiertAm - lauf.GestartetAm).TotalSeconds / lauf.Verarbeitet * rest);
        return new NeuAuslesenLaufDto(
            lauf.Id,
            lauf.GestartetAm,
            lauf.BeendetAm,
            laeuft,
            lauf.AbbruchAngefordert,
            laeuft || lauf.BeendetAm is not null ? lauf.Abbruchgrund : lauf.Abbruchgrund ?? GrundUnterbrochen,
            lauf.Gesamt,
            lauf.Verarbeitet,
            lauf.AnzahlFehler,
            lauf.AnzahlUebersprungen,
            restSekunden,
            (int)Math.Max(0, Math.Round((ende - lauf.GestartetAm).TotalSeconds)),
            lauf.LetzterFehler,
            [.. lauf.FehlerBelege]);
    }
}
