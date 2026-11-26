using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.UstIdPruefung;

/// <summary>
/// Prüft USt-IdNr. über VIES und speichert das Ergebnis am Korrespondenten. Ändert sich der Status, werden die Befunde
/// seiner Belege neu berechnet; eine ungültige Nummer meldet die Glocke.
/// </summary>
public sealed class UstIdPruefDienst(
    BiberDbContext db,
    IViesClient vies,
    IPruefDienst pruefDienst,
    IEnumerable<IBenachrichtigungsDienst> benachrichtigungen,
    TimeProvider zeit,
    ILogger<UstIdPruefDienst> log) : IUstIdPruefDienst
{
    /// <summary>Gültige und ungültige Ergebnisse werden nach dieser Zeit erneut geprüft.</summary>
    public static readonly TimeSpan Gueltigkeit = TimeSpan.FromDays(180);

    /// <summary>Nach einer Störung bei VIES wird frühestens nach dieser Zeit wiederholt.</summary>
    public static readonly TimeSpan Wiederholung = TimeSpan.FromHours(1);

    public async Task<bool> PruefenAsync(Guid korrespondentId, CancellationToken ct)
    {
        var korrespondent = await db.Korrespondenten.FirstOrDefaultAsync(k => k.Id == korrespondentId, ct);
        if (korrespondent is null)
        {
            return false;
        }

        await PruefenAsync(korrespondent, ct);
        return true;
    }

    /// <summary>Prüft höchstens <paramref name="max"/> Korrespondenten, deren Ergebnis fehlt oder veraltet ist.</summary>
    /// <returns>Anzahl geprüfter Korrespondenten.</returns>
    public async Task<int> FaelligePruefenAsync(int max, CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var veraltet = jetzt - Gueltigkeit;
        var wiederholen = jetzt - Wiederholung;
        var faellige = await db.Korrespondenten
            .Where(k => k.UstIdNr != null
                && (k.UstIdGeprueftNummer != k.UstIdNr
                    || k.UstIdGeprueftAm == null
                    || k.UstIdGeprueftAm < veraltet
                    || (k.UstIdStatus == UstIdStatus.NichtErreichbar && k.UstIdGeprueftAm < wiederholen)))
            .OrderBy(k => k.UstIdGeprueftAm)
            .ThenBy(k => k.Id)
            .Take(max)
            .ToListAsync(ct);

        foreach (var korrespondent in faellige)
        {
            await PruefenAsync(korrespondent, ct);
        }

        return faellige.Count;
    }

    private async Task PruefenAsync(Korrespondent k, CancellationToken ct)
    {
        var vorher = k.AktuellerUstIdStatus();
        var nummer = k.UstIdNr;
        var ergebnis = nummer is null
            ? new ViesErgebnis(UstIdStatus.Ungeprueft)
            : UstIdZerlegung.Zerlegen(nummer) is { } teile
                ? await vies.PruefenAsync(teile.Land, teile.Nummer, ct)
                : new ViesErgebnis(UstIdStatus.NichtPruefbar);

        k.UstIdStatus = ergebnis.Status;
        k.UstIdGeprueftNummer = nummer;
        k.UstIdGeprueftAm = nummer is null ? null : zeit.GetUtcNow();
        if (ergebnis.Status != UstIdStatus.NichtErreichbar)
        {
            k.UstIdAmtlicherName = ergebnis.Name;
            k.UstIdAmtlicheAnschrift = ergebnis.Anschrift;
        }

        if (ergebnis.Status == UstIdStatus.NichtErreichbar)
        {
            log.LogWarning("VIES-Prüfung für Korrespondent {Korrespondent} nicht möglich: {Fehler}", k.Id, ergebnis.Fehler);
        }
        else
        {
            log.LogInformation("USt-IdNr. von Korrespondent {Korrespondent} geprüft: {Status}", k.Id, ergebnis.Status);
        }

        if (ergebnis.Status != vorher)
        {
            var belege = await db.Belege
                .Where(b => b.KorrespondentId == k.Id && b.Richtung == Richtung.Eingang && b.Status == BelegStatus.Fertig)
                .ToListAsync(ct);
            foreach (var beleg in belege)
            {
                beleg.Korrespondent = k;
                await pruefDienst.PruefenAsync(beleg, null, ct);
            }
        }

        await db.SaveChangesAsync(ct);

        if (ergebnis.Status == UstIdStatus.Ungueltig && vorher != UstIdStatus.Ungueltig)
        {
            foreach (var dienst in benachrichtigungen)
            {
                await dienst.MeldenAsync(
                    new NeueBenachrichtigung(
                        BenachrichtigungArt.UstIdUngueltig,
                        Schweregrad.Warnung,
                        "USt-IdNr. ungültig",
                        $"Die USt-IdNr. {nummer} von {k.Name} ist laut VIES nicht gültig.",
                        Link: $"/korrespondenten/{k.Id}",
                        EntdoppelungsSchluessel: $"ustid-ungueltig:{k.Id}:{nummer}"),
                    ct);
            }
        }
    }
}
