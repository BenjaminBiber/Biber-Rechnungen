using Biber.Invoice.Domain.Pipeline;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>Trägt beim Start jeden registrierten <see cref="IWiederkehrenderJob"/> per <c>AddOrUpdate</c> in Hangfire ein.</summary>
public sealed class WiederkehrendeJobsEintragen(IServiceProvider dienste) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = dienste.CreateScope();
        var jobs = scope.ServiceProvider.GetServices<IWiederkehrenderJob>().ToList();
        if (jobs.Count == 0)
        {
            return Task.CompletedTask; // ohne Jobs muss der Hangfire-Speicher nicht angefasst werden
        }

        var manager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        foreach (var job in jobs)
        {
            var id = job.Id;
            manager.AddOrUpdate<WiederkehrenderJobAusfuehrer>(
                id,
                a => a.AusfuehrenAsync(id, CancellationToken.None),
                job.Cron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
