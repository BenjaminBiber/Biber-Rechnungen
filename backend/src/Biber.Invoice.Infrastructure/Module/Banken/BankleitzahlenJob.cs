using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Banken;

/// <summary>
/// Hält die Bankleitzahlen aktuell: Läuft stündlich, holt aber nur, solange die Tabelle leer ist (z. B. nach der
/// Installation), stündlich und sonst wöchentlich bei der Bundesbank nach. Geladen wird nur eine neue Datei
/// (vierteljährlich, siehe <see cref="BankleitzahlenDienst.AbrufenAsync"/>).
/// </summary>
public sealed class BankleitzahlenJob(BankleitzahlenDienst dienst, BiberDbContext db, TimeProvider zeit) : IWiederkehrenderJob
{
    public const string JobId = "bankleitzahlen";

    public static readonly TimeSpan AbstandOhneBanken = TimeSpan.FromHours(1);
    public static readonly TimeSpan AbstandMitBanken = TimeSpan.FromDays(7);

    /// <summary>Spielraum, damit der stündliche Lauf nicht knapp an der Stunde vorbeiprüft.</summary>
    private static readonly TimeSpan Spielraum = TimeSpan.FromMinutes(5);

    public string Id => JobId;

    public string Cron => "23 * * * *";

    public async Task AusfuehrenAsync(CancellationToken ct)
    {
        var stand = await db.BankleitzahlenStaende.AsNoTracking().SingleOrDefaultAsync(ct);
        var abstand = stand is { Anzahl: > 0 } ? AbstandMitBanken : AbstandOhneBanken;
        if (stand?.LetzterAbrufAm is { } zuletzt && zeit.GetUtcNow() - zuletzt < abstand - Spielraum)
        {
            return;
        }

        await dienst.AbrufenAsync(erzwingen: false, ct);
    }
}
