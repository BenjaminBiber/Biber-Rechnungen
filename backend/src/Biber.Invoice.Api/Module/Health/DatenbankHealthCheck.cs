using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Biber.Invoice.Api.Module.Health;

public sealed class DatenbankHealthCheck(BiberDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Datenbank nicht erreichbar.");
}
