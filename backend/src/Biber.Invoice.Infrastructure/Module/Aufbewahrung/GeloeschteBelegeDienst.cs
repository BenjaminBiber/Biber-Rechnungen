using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Belege.Schreiben;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Aufbewahrung;

/// <summary>
/// Gelöschte Belege: Ein Admin kann einen falsch zugeordneten Beleg trotz Löschsperre löschen. Gelöscht wird nichts –
/// der Beleg wird über <see cref="Beleg.GeloeschtAm"/> ausgeblendet (globaler Abfragefilter), seine Originale ziehen in
/// der Ablage nach <c>_Geloescht/</c> um. Wiederherstellen macht beides rückgängig.
/// </summary>
public sealed class GeloeschteBelegeDienst(
    BiberDbContext db,
    IAblageSchreiber ablage,
    IAuditSchreiber audit,
    IAktuellerBenutzer benutzer,
    TimeProvider zeit,
    ILogger<GeloeschteBelegeDienst> log)
{
    public const string AuditAktionGeloescht = "FalschZugeordnetGeloescht";
    public const string AuditAktionWiederhergestellt = "Wiederhergestellt";
    public const int MaxBemerkung = 2000;

    public async Task<GeloeschterBelegDto[]> ListeAsync(CancellationToken ct) =>
        await db.Belege.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.GeloeschtAm != null)
            .OrderByDescending(b => b.GeloeschtAm)
            .ThenBy(b => b.Id)
            .Select(b => new GeloeschterBelegDto(
                b.Id,
                b.Titel,
                b.Rechnungsnummer,
                b.Rechnungsdatum,
                b.Korrespondent == null ? null : b.Korrespondent.Name,
                b.Brutto,
                b.Waehrung,
                b.LoeschGrund ?? LoeschGrund.FalschZugeordnet,
                b.LoeschBemerkung,
                b.GeloeschtAm!.Value,
                db.Users.Where(u => u.Id == b.GeloeschtVonId).Select(u => u.Anzeigename).FirstOrDefault(),
                b.Dateien.Where(d => d.Rolle == DateiRolle.Original).OrderBy(d => d.AbgelegtAm).Select(d => (Guid?)d.Id)
                    .FirstOrDefault()))
            .ToArrayAsync(ct);

    /// <returns><c>false</c>, wenn es den (nicht gelöschten) Beleg nicht gibt.</returns>
    /// <exception cref="BelegValidierungsAusnahme">Grund fehlt oder Bemerkung zu lang.</exception>
    /// <exception cref="BelegKonfliktAusnahme">Der Beleg wird gerade verarbeitet.</exception>
    public async Task<bool> LoeschenAsync(Guid id, BelegLoeschenDto anfrage, CancellationToken ct)
    {
        if (anfrage.Grund is not LoeschGrund.FalschZugeordnet)
        {
            throw BelegValidierungsAusnahme.Fuer("grund",
                "Ein Beleg darf nur gelöscht werden, wenn er falsch zugeordnet ist (Grund „FalschZugeordnet“).");
        }

        var bemerkung = string.IsNullOrWhiteSpace(anfrage.Bemerkung) ? null : anfrage.Bemerkung.Trim();
        if (bemerkung?.Length > MaxBemerkung)
        {
            throw BelegValidierungsAusnahme.Fuer("bemerkung", $"Die Bemerkung darf höchstens {MaxBemerkung} Zeichen lang sein.");
        }

        var beleg = await db.Belege.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return false;
        }

        if (beleg.Status == BelegStatus.InVerarbeitung)
        {
            throw new BelegKonfliktAusnahme("Der Beleg wird gerade verarbeitet und kann erst danach gelöscht werden.");
        }

        var verschoben = await ablage.InGeloeschtVerschiebenAsync(beleg, ct);
        beleg.GeloeschtAm = zeit.GetUtcNow();
        beleg.GeloeschtVonId = benutzer.Id;
        beleg.LoeschGrund = anfrage.Grund;
        beleg.LoeschBemerkung = bemerkung;
        audit.Notieren(nameof(Beleg), id.ToString(), id, AuditAktionGeloescht,
            bemerkung is null ? "Als falsch zugeordnet gelöscht" : $"Als falsch zugeordnet gelöscht: {bemerkung}");
        await SpeichernAsync(verschoben, ct);
        log.LogInformation("Beleg {Beleg} als falsch zugeordnet gelöscht", id);
        return true;
    }

    /// <returns><c>false</c>, wenn es keinen gelöschten Beleg mit dieser ID gibt.</returns>
    public async Task<bool> WiederherstellenAsync(Guid id, CancellationToken ct)
    {
        var beleg = await db.Belege.IgnoreQueryFilters()
            .Include(b => b.Korrespondent)
            .FirstOrDefaultAsync(b => b.Id == id && b.GeloeschtAm != null, ct);
        if (beleg is null)
        {
            return false;
        }

        var verschoben = await ablage.VerschiebenAsync(beleg, ct);
        beleg.GeloeschtAm = null;
        beleg.GeloeschtVonId = null;
        beleg.LoeschGrund = null;
        beleg.LoeschBemerkung = null;
        audit.Notieren(nameof(Beleg), id.ToString(), id, AuditAktionWiederhergestellt, "Gelöschten Beleg wiederhergestellt");
        await SpeichernAsync(verschoben, ct);
        log.LogInformation("Gelöschter Beleg {Beleg} wiederhergestellt", id);
        return true;
    }

    private async Task SpeichernAsync(IReadOnlyList<AblageVerschiebung> verschoben, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            ablage.Zuruecknehmen(verschoben);
            throw;
        }
    }
}
