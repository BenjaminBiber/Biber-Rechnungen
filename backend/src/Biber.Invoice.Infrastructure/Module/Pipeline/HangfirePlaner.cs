using Biber.Invoice.Domain.Pipeline;
using Hangfire;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>Plant den <see cref="VerarbeitungsJob"/> in Hangfire ein (Queue über <see cref="QueueAttribute"/>).</summary>
public sealed class HangfirePlaner(IBackgroundJobClient jobs) : IVerarbeitungsPlaner
{
    public void Einplanen(Guid importVorgangId, TimeSpan? verzoegerung = null)
    {
        if (verzoegerung is { } wartezeit && wartezeit > TimeSpan.Zero)
        {
            jobs.Schedule<VerarbeitungsJob>(j => j.AusfuehrenAsync(importVorgangId, CancellationToken.None), wartezeit);
        }
        else
        {
            jobs.Enqueue<VerarbeitungsJob>(j => j.AusfuehrenAsync(importVorgangId, CancellationToken.None));
        }
    }
}
