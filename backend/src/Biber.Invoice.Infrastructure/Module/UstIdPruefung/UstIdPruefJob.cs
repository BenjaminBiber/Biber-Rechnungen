using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.UstIdPruefung;

/// <summary>Prüft alle 15 Minuten eine Handvoll neuer oder veralteter USt-IdNr. über VIES, sofern eingeschaltet.</summary>
public sealed class UstIdPruefJob(UstIdPruefDienst dienst, IEinstellungen einstellungen, ILogger<UstIdPruefJob> log) : IWiederkehrenderJob
{
    /// <summary>Begrenzt die Anfragen je Lauf, damit VIES nicht drosselt.</summary>
    public const int MaxJeLauf = 20;

    public string Id => "ustid-pruefung";

    public string Cron => "*/15 * * * *";

    public async Task AusfuehrenAsync(CancellationToken ct)
    {
        if (!(await einstellungen.LesenAsync<PruefEinstellungen>(ct)).UstIdOnlinePruefen)
        {
            return;
        }

        var anzahl = await dienst.FaelligePruefenAsync(MaxJeLauf, ct);
        if (anzahl > 0)
        {
            log.LogInformation("{Anzahl} USt-IdNr. über VIES geprüft", anzahl);
        }
    }
}
