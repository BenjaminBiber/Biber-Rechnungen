using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.Infrastruktur;

/// <summary>
/// <see cref="IVerarbeitungsPlaner"/> für Tests: führt den <see cref="VerarbeitungsJob"/> ohne Verzögerung sofort
/// im selben Prozess aus. Verzögerte Einplanungen (Backoff) werden nur in <see cref="Verzoegert"/> notiert;
/// der Test stellt die Uhr vor und ruft <see cref="AusfuehrenAsync"/> selbst auf.
/// </summary>
public sealed class SofortPlaner(IServiceProvider dienste) : IVerarbeitungsPlaner
{
    private readonly List<(Guid Id, TimeSpan Verzoegerung)> _verzoegert = [];

    public IReadOnlyList<(Guid Id, TimeSpan Verzoegerung)> Verzoegert
    {
        get
        {
            lock (_verzoegert)
            {
                return [.. _verzoegert];
            }
        }
    }

    public void Einplanen(Guid importVorgangId, TimeSpan? verzoegerung = null)
    {
        if (verzoegerung is { } wartezeit && wartezeit > TimeSpan.Zero)
        {
            lock (_verzoegert)
            {
                _verzoegert.Add((importVorgangId, wartezeit));
            }

            return;
        }

        // Eigener Thread, damit das Warten keinen Synchronisationskontext blockiert.
        Task.Run(() => AusfuehrenAsync(importVorgangId)).GetAwaiter().GetResult();
    }

    public async Task AusfuehrenAsync(Guid importVorgangId)
    {
        await using var scope = dienste.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<VerarbeitungsJob>().AusfuehrenAsync(importVorgangId, CancellationToken.None);
    }
}

public static class SofortVerarbeitung
{
    /// <summary>Neue Fabrik, in der <see cref="SofortPlaner"/> den Hangfire-Planer ersetzt.</summary>
    public static ApiFabrik MitSofortVerarbeitung(this ApiFabrik fabrik) =>
        fabrik.MitDiensten(dienste =>
        {
            dienste.RemoveAll<IVerarbeitungsPlaner>();
            dienste.AddSingleton<SofortPlaner>();
            dienste.AddSingleton<IVerarbeitungsPlaner>(sp => sp.GetRequiredService<SofortPlaner>());
        });

    public static SofortPlaner Planer(this ApiFabrik fabrik) => fabrik.Services.GetRequiredService<SofortPlaner>();

    /// <summary>Nimmt eine Datei über <see cref="IImportDienst.AnnehmenAsync"/> an und liefert den gespeicherten Vorgang.</summary>
    public static async Task<ImportVorgang> ImportiereAsync(
        this ApiFabrik fabrik,
        string dateiname,
        byte[] inhalt,
        ImportQuelle quelle = ImportQuelle.Upload,
        ImportHerkunft? herkunft = null)
    {
        Guid id;
        using (var scope = fabrik.ErzeugeScope())
        {
            var dienst = scope.ServiceProvider.GetRequiredService<IImportDienst>();
            using var strom = new MemoryStream(inhalt);
            id = (await dienst.AnnehmenAsync(strom, dateiname, quelle, herkunft ?? new ImportHerkunft(), CancellationToken.None)).Id;
        }

        return await fabrik.VorgangAsync(id);
    }

    /// <summary>Lädt einen Import-Vorgang frisch aus der Datenbank.</summary>
    public static async Task<ImportVorgang> VorgangAsync(this ApiFabrik fabrik, Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.ImportVorgaenge.AsNoTracking().SingleAsync(v => v.Id == id);
    }
}
