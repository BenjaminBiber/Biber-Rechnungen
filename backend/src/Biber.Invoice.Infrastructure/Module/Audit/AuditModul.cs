using Biber.Invoice.Domain.Module;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Audit;

/// <summary>Änderungsprotokoll: Interceptor, Schreiber für explizite Ereignisse und Historie eines Belegs.</summary>
public sealed class AuditModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddScoped<AuditSchreiber>();
        dienste.AddScoped<IAuditSchreiber>(sp => sp.GetRequiredService<AuditSchreiber>());
        dienste.AddScoped<IInterceptor, AuditInterceptor>();
        dienste.AddScoped<HistorieLeser>();
    }
}
