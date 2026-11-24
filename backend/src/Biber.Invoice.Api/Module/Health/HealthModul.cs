using Biber.Invoice.Domain.Module;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Biber.Invoice.Api.Module.Health;

/// <summary><c>GET /health/live</c> (Prozess läuft) und <c>GET /health/ready</c> (Datenbank erreichbar), beide anonym.</summary>
public sealed class HealthModul : IDienstModul, IEndpunktModul
{
    private const string Bereit = "ready";

    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddHealthChecks()
            .AddCheck<DatenbankHealthCheck>("datenbank", tags: [Bereit]);
    }

    public void Zuordnen(IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(Bereit) })
            .AllowAnonymous();
    }
}
