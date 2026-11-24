using Biber.Invoice.Domain.Pipeline;
using Hangfire;

namespace Biber.Invoice.Infrastructure.Module.Integritaet;

/// <summary>Nächtliche Integritätsprüfung der Ablage (03:00).</summary>
public sealed class IntegritaetsJob(IntegritaetsPruefer pruefer) : IWiederkehrenderJob
{
    public string Id => "integritaet";

    public string Cron => "0 3 * * *";

    public Task AusfuehrenAsync(CancellationToken ct) => pruefer.PruefenAsync(ct);
}

/// <summary>Startet eine Integritätsprüfung im Hintergrund.</summary>
public interface IIntegritaetsStarter
{
    void Starten();
}

/// <summary>Reiht den <see cref="IntegritaetsJob"/> in Hangfire ein.</summary>
public sealed class HangfireIntegritaetsStarter(IBackgroundJobClient jobs) : IIntegritaetsStarter
{
    public void Starten() => jobs.Enqueue<IntegritaetsJob>(j => j.AusfuehrenAsync(CancellationToken.None));
}
