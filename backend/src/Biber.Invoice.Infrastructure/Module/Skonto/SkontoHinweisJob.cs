using System.Globalization;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Belege.Lesen;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Skonto;

/// <summary>
/// Meldet stündlich offene Rechnungen, deren Skontofrist in den nächsten <see cref="PruefEinstellungen.SkontoVorlaufTage"/>
/// Tagen (einschließlich heute) endet. Je Beleg und Frist erscheint der Hinweis nur einmal.
/// </summary>
public sealed class SkontoHinweisJob(
    BiberDbContext db,
    IEinstellungen einstellungen,
    IEnumerable<IBenachrichtigungsDienst> benachrichtigungen,
    TimeProvider zeit,
    ILogger<SkontoHinweisJob> log) : IWiederkehrenderJob
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    public string Id => "skonto-hinweis";

    public string Cron => "0 * * * *";

    public async Task AusfuehrenAsync(CancellationToken ct)
    {
        var vorlauf = (await einstellungen.LesenAsync<PruefEinstellungen>(ct)).SkontoVorlaufTage;
        if (vorlauf <= 0)
        {
            return;
        }

        var heute = Heute((await einstellungen.LesenAsync<AllgemeinEinstellungen>(ct)).Zeitzone);
        var bis = heute.AddDays(vorlauf);
        var faellige = await db.Belege
            .AsNoTracking()
            .Where(b => b.Status == BelegStatus.Fertig && b.Belegart == Belegart.Rechnung)
            .NurOffene()
            .Where(b => b.SkontoBis >= heute && b.SkontoBis <= bis)
            .OrderBy(b => b.SkontoBis)
            .Select(b => new
            {
                b.Id,
                b.Rechnungsnummer,
                Korrespondent = b.Korrespondent == null ? null : b.Korrespondent.Name,
                b.SkontoProzent,
                b.SkontoBis,
                b.SkontoBetrag,
                b.Waehrung,
            })
            .ToListAsync(ct);

        foreach (var b in faellige)
        {
            var rechnung = b.Rechnungsnummer is null ? "Rechnung" : $"Rechnung {b.Rechnungsnummer}";
            var von = b.Korrespondent is null ? "" : $" von {b.Korrespondent}";
            var ersparnis = b.SkontoBetrag is { } betrag ? $" (spart {betrag.ToString("N2", Deutsch)} {b.Waehrung})" : "";
            var frist = b.SkontoBis == heute ? "nur noch heute" : $"nur noch bis {b.SkontoBis!.Value.ToString("dd.MM.yyyy", Deutsch)}";
            foreach (var dienst in benachrichtigungen)
            {
                await dienst.MeldenAsync(
                    new NeueBenachrichtigung(
                        BenachrichtigungArt.SkontoLaeuftAb,
                        Schweregrad.Warnung,
                        "Skonto läuft ab",
                        $"{rechnung}{von}: {b.SkontoProzent!.Value.ToString("0.##", Deutsch)} % Skonto{ersparnis} {frist}.",
                        Link: $"/belege/{b.Id}",
                        EntdoppelungsSchluessel: $"skonto:{b.Id}:{b.SkontoBis:yyyy-MM-dd}"),
                    ct);
            }
        }

        if (faellige.Count > 0)
        {
            log.LogInformation("Skonto-Hinweis für {Anzahl} Rechnungen geprüft", faellige.Count);
        }
    }

    private DateOnly Heute(string zeitzone)
    {
        var jetzt = zeit.GetUtcNow();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(zeitzone, out var zone))
        {
            jetzt = TimeZoneInfo.ConvertTime(jetzt, zone);
        }

        return DateOnly.FromDateTime(jetzt.DateTime);
    }
}
