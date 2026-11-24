using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>
/// Verarbeitungspipeline: Hangfire (Postgres, Schema <c>hangfire</c>), Import-Annahme, Staging, Orchestrator.
/// Der Hangfire-Server läuft nur bei <c>Hangfire:ServerAktiv</c> (Standard <c>true</c>; Tests schalten ihn ab).
/// </summary>
public sealed class PipelineModul : IDienstModul
{
    public const string QueueVerarbeitung = "verarbeitung";

    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddHangfire((sp, hangfire) =>
        {
            // Erst beim Auflösen lesen, damit Überschreibungen (z. B. in Tests) greifen.
            var verbindung = sp.GetRequiredService<IConfiguration>().GetConnectionString("Datenbank");
            hangfire
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(
                    o => o.UseNpgsqlConnection(verbindung),
                    new PostgreSqlStorageOptions { SchemaName = "hangfire" });
        });

        if (konfiguration.GetValue("Hangfire:ServerAktiv", true))
        {
            dienste.AddHangfireServer(o => o.Queues = [QueueVerarbeitung, "default"]);
        }

        dienste.AddSingleton<IStagingSpeicher, StagingSpeicher>();
        dienste.AddSingleton<IVerarbeitungsPlaner, HangfirePlaner>();
        dienste.AddScoped<IImportDienst, ImportDienst>();
        dienste.AddScoped<IVerarbeitungsStarter, VerarbeitungsStarter>();
        dienste.AddScoped<VerarbeitungsJob>();
        dienste.AddScoped<IPipelineSchritt, ExtraktionUebernehmenSchritt>();
        dienste.AddScoped<WiederkehrenderJobAusfuehrer>();
        dienste.AddHostedService<WiederkehrendeJobsEintragen>();
    }
}
