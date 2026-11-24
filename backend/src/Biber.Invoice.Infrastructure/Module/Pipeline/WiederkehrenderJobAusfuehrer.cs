using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>Hangfire-Einstieg für <see cref="IWiederkehrenderJob"/>: sucht den Job zur Kennung und führt ihn aus.</summary>
public sealed class WiederkehrenderJobAusfuehrer(IEnumerable<IWiederkehrenderJob> jobs)
{
    public Task AusfuehrenAsync(string id, CancellationToken ct)
    {
        var job = jobs.FirstOrDefault(j => j.Id == id)
            ?? throw new InvalidOperationException($"Wiederkehrender Job „{id}“ ist nicht registriert.");
        return job.AusfuehrenAsync(ct);
    }
}
