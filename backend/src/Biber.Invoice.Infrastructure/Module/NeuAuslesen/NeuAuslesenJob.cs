using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.Infrastructure.Persistenz.Konfiguration;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.NeuAuslesen;

/// <summary>
/// Arbeitet einen <see cref="NeuAuslesenLauf"/> ab: liest jeden Beleg, der beim Start vorhanden war, nacheinander
/// mit <see cref="IVerarbeitungsStarter"/> neu aus (je Beleg ein eigener Scope) und schreibt nach jedem Beleg den
/// Stand. Nach einem Neustart setzt Hangfire den Job fort; er macht dann bei <see cref="NeuAuslesenLauf.Verarbeitet"/>
/// weiter. Erreicht Document Intelligence das Monatslimit, hält der Lauf an, statt jeden weiteren Beleg scheitern zu lassen.
/// </summary>
public sealed class NeuAuslesenJob(
    BiberDbContext db,
    IServiceScopeFactory scopes,
    TimeProvider zeit,
    ILogger<NeuAuslesenJob> log)
{
    public const int MaxFehlerBelege = 100;

    private static readonly SemaphoreSlim Sperre = new(1, 1);

    private enum Ausgang
    {
        Ausgelesen,
        Uebersprungen,
        Fehler,
        Angehalten,
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task AusfuehrenAsync(Guid laufId, CancellationToken ct)
    {
        if (!await Sperre.WaitAsync(0, ct))
        {
            log.LogWarning("Erneutes Auslesen (Lauf {Lauf}) übersprungen: es läuft bereits ein Lauf", laufId);
            return;
        }

        try
        {
            await DurchfuehrenAsync(laufId, ct);
        }
        finally
        {
            Sperre.Release();
        }
    }

    private async Task DurchfuehrenAsync(Guid laufId, CancellationToken ct)
    {
        var lauf = await db.NeuAuslesenLaeufe.FirstOrDefaultAsync(l => l.Id == laufId, ct);
        if (lauf is null || lauf.BeendetAm is not null)
        {
            return;
        }

        var belegIds = await NeuAuslesenDienst.Belege(db, lauf.GestartetAm)
            .OrderBy(b => b.Id)
            .Select(b => b.Id)
            .ToListAsync(ct);
        if (lauf.Verarbeitet > 0)
        {
            log.LogInformation("Erneutes Auslesen (Lauf {Lauf}) wird ab Beleg {Stand} von {Gesamt} fortgesetzt", lauf.Id, lauf.Verarbeitet + 1, lauf.Gesamt);
        }

        foreach (var belegId in belegIds.Skip(lauf.Verarbeitet))
        {
            await db.Entry(lauf).ReloadAsync(ct);
            if (lauf.AbbruchAngefordert || lauf.BeendetAm is not null)
            {
                break;
            }

            var (ausgang, meldung) = await AuslesenAsync(belegId, ct);
            lauf.Verarbeitet++;
            lauf.AktualisiertAm = zeit.GetUtcNow();
            switch (ausgang)
            {
                case Ausgang.Uebersprungen:
                    lauf.AnzahlUebersprungen++;
                    break;
                case Ausgang.Fehler or Ausgang.Angehalten:
                    lauf.AnzahlFehler++;
                    lauf.LetzterFehler = Kuerzen(meldung);
                    if (lauf.FehlerBelege.Count < MaxFehlerBelege)
                    {
                        lauf.FehlerBelege = [.. lauf.FehlerBelege, belegId];
                    }

                    break;
            }

            if (ausgang == Ausgang.Angehalten)
            {
                lauf.Abbruchgrund = Kuerzen(meldung);
                lauf.BeendetAm = lauf.AktualisiertAm;
            }

            await db.SaveChangesAsync(ct);
            if (lauf.BeendetAm is not null)
            {
                break;
            }
        }

        await db.Entry(lauf).ReloadAsync(ct);
        if (lauf.BeendetAm is null)
        {
            lauf.BeendetAm = zeit.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        log.LogInformation(
            "Erneutes Auslesen beendet: {Verarbeitet} von {Gesamt} Belegen, {Fehler} Fehler, {Uebersprungen} übersprungen{Grund} (Lauf {Lauf})",
            lauf.Verarbeitet, lauf.Gesamt, lauf.AnzahlFehler, lauf.AnzahlUebersprungen,
            lauf.Abbruchgrund is { } grund ? $" – {grund}" : string.Empty, lauf.Id);
    }

    private async Task<(Ausgang Ausgang, string? Meldung)> AuslesenAsync(Guid belegId, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var dienste = scope.ServiceProvider;
        var status = await dienste.GetRequiredService<BiberDbContext>().Belege
            .Where(b => b.Id == belegId)
            .Select(b => (BelegStatus?)b.Status)
            .FirstOrDefaultAsync(ct);
        if (status is null or BelegStatus.InVerarbeitung)
        {
            return (Ausgang.Uebersprungen, null);
        }

        try
        {
            await dienste.GetRequiredService<IVerarbeitungsStarter>().ErneutVerarbeitenAsync(belegId, ct);
            return (Ausgang.Ausgelesen, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (VerarbeitungAbgebrochenException ex) when (ex.Ausgang == SchrittAusgang.Pausiert)
        {
            log.LogWarning("Erneutes Auslesen angehalten bei Beleg {Beleg}: {Schritt}: {Grund}", belegId, ex.Schritt, ex.Grund);
            return (Ausgang.Angehalten, $"{ex.Schritt}: {ex.Grund}");
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Erneutes Auslesen von Beleg {Beleg} fehlgeschlagen: {Fehler}", belegId, ex.Message);
            return (Ausgang.Fehler, ex.Message);
        }
    }

    private static string? Kuerzen(string? text) =>
        text is { Length: > NeuAuslesenLaufKonfiguration.MaxGrundLaenge } ? text[..NeuAuslesenLaufKonfiguration.MaxGrundLaenge] : text;
}

/// <summary>Startet einen <see cref="NeuAuslesenJob"/> im Hintergrund.</summary>
public interface INeuAuslesenStarter
{
    void Starten(Guid laufId);
}

/// <summary>Reiht den <see cref="NeuAuslesenJob"/> in Hangfire ein (Queue „default“, damit neue Importe nicht warten).</summary>
public sealed class HangfireNeuAuslesenStarter(IBackgroundJobClient jobs) : INeuAuslesenStarter
{
    public void Starten(Guid laufId) => jobs.Enqueue<NeuAuslesenJob>(j => j.AusfuehrenAsync(laufId, CancellationToken.None));
}
