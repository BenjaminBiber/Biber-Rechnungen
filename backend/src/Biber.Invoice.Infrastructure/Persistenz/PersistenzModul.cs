using Biber.Invoice.Domain.Module;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Persistenz;

/// <summary>
/// Registriert <see cref="BiberDbContext"/> gegen Postgres (<c>ConnectionStrings:Datenbank</c>).
/// Module steuern Interceptoren bei, indem sie <see cref="IInterceptor"/> im DI registrieren.
/// </summary>
public sealed class PersistenzModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddDbContext<BiberDbContext>((sp, optionen) =>
        {
            // Erst beim Auflösen lesen, damit Überschreibungen (z. B. in Tests) greifen.
            var verbindung = sp.GetRequiredService<IConfiguration>().GetConnectionString("Datenbank");
            optionen.UseNpgsql(verbindung);
            optionen.AddInterceptors(sp.GetServices<IInterceptor>());
        });
    }
}
