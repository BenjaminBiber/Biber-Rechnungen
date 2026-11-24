using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Infrastructure.Module.AutoMatching;

/// <summary>Nächtliches Training der Auto-Matching-Modelle.</summary>
public sealed class AutoMatchingJob(AutoMatchingTrainer trainer) : IWiederkehrenderJob
{
    public string Id => "auto-matching";

    public string Cron => "30 2 * * *";

    public Task AusfuehrenAsync(CancellationToken ct) => trainer.TrainierenAsync(ct);
}
