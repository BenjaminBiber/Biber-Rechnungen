using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.DocumentIntelligence;

/// <summary>Setzt zum Monatswechsel die wegen des DI-Limits pausierten Vorgänge fort.</summary>
public sealed class DiMonatswechselJob(IImportDienst import, ILogger<DiMonatswechselJob> log) : IWiederkehrenderJob
{
    public string Id => "di-monatswechsel";

    public string Cron => "5 0 1 * *";

    public async Task AusfuehrenAsync(CancellationToken ct)
    {
        var anzahl = await import.PausierteFortsetzenAsync(ct);
        log.LogInformation("Monatswechsel: {Anzahl} pausierte Vorgänge fortgesetzt", anzahl);
    }
}
