namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Ein zeitgesteuerter Job; das PipelineModul trägt jeden registrierten Job beim Start in Hangfire ein.</summary>
public interface IWiederkehrenderJob
{
    /// <summary>Eindeutige Kennung in Hangfire, z. B. „mail-abruf“.</summary>
    string Id { get; }

    /// <summary>Cron-Ausdruck, z. B. <c>*/5 * * * *</c>.</summary>
    string Cron { get; }

    Task AusfuehrenAsync(CancellationToken ct);
}
