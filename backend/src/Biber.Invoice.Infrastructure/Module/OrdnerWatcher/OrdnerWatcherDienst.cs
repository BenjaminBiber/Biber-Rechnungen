using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.OrdnerWatcher;

/// <summary>
/// Überwacht den Eingangsordner: Ereignisse des <see cref="FileSystemWatcher"/> lösen (entprellt) eine Abtastung aus,
/// zusätzlich wird alle <see cref="OrdnerEinstellungen.PollingSekunden"/> abgetastet, weil Netzlaufwerke und
/// Docker Desktop Ereignisse unzuverlässig melden.
/// </summary>
public sealed class OrdnerWatcherDienst(
    OrdnerAbtaster abtaster,
    IServiceScopeFactory scopes,
    ILogger<OrdnerWatcherDienst> log) : BackgroundService
{
    private static readonly TimeSpan Entprellung = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Wiederholung = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _signal = new(0, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                watcher ??= WatcherStarten();
                if (watcher is not null)
                {
                    await AbtastenAsync(stoppingToken);
                }

                var warten = watcher is null ? Wiederholung : await PollingAsync(stoppingToken);
                if (await _signal.WaitAsync(warten, stoppingToken))
                {
                    await Task.Delay(Entprellung, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            watcher?.Dispose();
        }
    }

    private FileSystemWatcher? WatcherStarten()
    {
        try
        {
            Directory.CreateDirectory(abtaster.Eingang);
            var watcher = new FileSystemWatcher(abtaster.Eingang)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            };
            watcher.Created += (_, _) => Signalisieren();
            watcher.Changed += (_, _) => Signalisieren();
            watcher.Renamed += (_, _) => Signalisieren();
            watcher.Error += (_, e) =>
            {
                log.LogWarning(e.GetException(), "Ordner-Watcher meldet einen Fehler; Polling läuft weiter");
                Signalisieren();
            };
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or ArgumentException)
        {
            log.LogWarning(fehler, "Eingangsordner {Ordner} ist nicht erreichbar; neuer Versuch in {Sekunden} s", abtaster.Eingang, Wiederholung.TotalSeconds);
            return null;
        }
    }

    private void Signalisieren()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // es steht schon eine Abtastung an
        }
    }

    private async Task AbtastenAsync(CancellationToken ct)
    {
        try
        {
            await abtaster.AbtastenAsync(ct);
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException)
        {
            log.LogWarning(fehler, "Abtastung des Eingangsordners fehlgeschlagen");
        }
    }

    private async Task<TimeSpan> PollingAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var e = await scope.ServiceProvider.GetRequiredService<IEinstellungen>().LesenAsync<OrdnerEinstellungen>(ct);
            return TimeSpan.FromSeconds(Math.Max(1, e.PollingSekunden));
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException)
        {
            log.LogWarning(fehler, "Einstellungen des Eingangsordners nicht lesbar");
            return Wiederholung;
        }
    }
}
