using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Mail;

/// <summary>
/// Läuft jede Minute und ruft das Postfach ab, wenn der Import aktiv und eingerichtet ist und seit dem letzten Abruf
/// <see cref="MailEinstellungen.AbrufintervallMinuten"/> vergangen sind. Läuft gerade ein Abruf, entfällt dieser Lauf.
/// </summary>
public sealed class MailAbrufJob(
    MailAbruf abruf,
    IServiceScopeFactory scopes,
    TimeProvider zeit,
    ILogger<MailAbrufJob> log) : IWiederkehrenderJob
{
    /// <summary>Spielraum, damit ein Intervall von 5 Minuten nicht durch Sekundenversatz zum Minutentakt 6 Minuten dauert.</summary>
    private static readonly TimeSpan Spielraum = TimeSpan.FromSeconds(30);

    public string Id => "mail-abruf";

    public string Cron => "* * * * *";

    public async Task AusfuehrenAsync(CancellationToken ct)
    {
        if (!await FaelligAsync(ct))
        {
            return;
        }

        try
        {
            if (await abruf.AbrufenFallsFreiAsync(ct) is null)
            {
                log.LogDebug("Mail-Abruf läuft bereits; dieser Lauf entfällt");
            }
        }
        catch (MailAbrufFehler)
        {
            // Protokolliert und im Status vermerkt; der nächste Versuch kommt nach dem Intervall.
        }
    }

    private async Task<bool> FaelligAsync(CancellationToken ct)
    {
        if (!abruf.Konfiguriert)
        {
            return false;
        }

        using var scope = scopes.CreateScope();
        var einstellungen = await scope.ServiceProvider.GetRequiredService<IEinstellungen>().LesenAsync<MailEinstellungen>(ct);
        if (!einstellungen.Aktiv)
        {
            return false;
        }

        var status = await MailStatusSpeicher.LesenAsync(scope.ServiceProvider.GetRequiredService<BiberDbContext>(), ct);
        var intervall = TimeSpan.FromMinutes(Math.Max(1, einstellungen.AbrufintervallMinuten));
        return status?.LetzterAbruf is not { } letzter || zeit.GetUtcNow() - letzter >= intervall - Spielraum;
    }
}
