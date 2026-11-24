using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Biber.Invoice.Infrastructure.Module.Benachrichtigungen;

/// <summary>
/// Legt Benachrichtigungen an. Mit <see cref="NeueBenachrichtigung.EntdoppelungsSchluessel"/> entsteht
/// höchstens ein Eintrag je Schlüssel, auch bei gleichzeitigen Aufrufen (eindeutiger Index).
/// </summary>
public sealed class BenachrichtigungsDienst(
    BiberDbContext db,
    TimeProvider zeit,
    ILogger<BenachrichtigungsDienst> logger) : IBenachrichtigungsDienst
{
    public const int TitelMaxLaenge = 200;
    public const int TextMaxLaenge = 1000;

    public async Task MeldenAsync(NeueBenachrichtigung meldung, CancellationToken ct)
    {
        var schluessel = meldung.EntdoppelungsSchluessel;
        if (schluessel is not null && await db.Benachrichtigungen.AnyAsync(b => b.EntdoppelungsSchluessel == schluessel, ct))
        {
            return;
        }

        var benachrichtigung = new Benachrichtigung
        {
            Id = Guid.NewGuid(),
            Art = meldung.Art,
            Schweregrad = meldung.Schweregrad,
            Titel = Kuerzen(meldung.Titel, TitelMaxLaenge),
            Text = Kuerzen(meldung.Text, TextMaxLaenge),
            Link = meldung.Link,
            NurAdmins = meldung.NurAdmins,
            EntdoppelungsSchluessel = schluessel,
            ErstelltAm = zeit.GetUtcNow(),
        };
        db.Benachrichtigungen.Add(benachrichtigung);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (schluessel is not null && ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Ein gleichzeitiger Aufruf war schneller: Der Eintrag zu diesem Schlüssel existiert bereits.
            db.Entry(benachrichtigung).State = EntityState.Detached;
            logger.LogDebug("Benachrichtigung mit Schlüssel {Schluessel} existiert bereits", schluessel);
        }
    }

    private static string Kuerzen(string wert, int maxLaenge) =>
        wert.Length <= maxLaenge ? wert : string.Concat(wert.AsSpan(0, maxLaenge - 1), "…");
}
