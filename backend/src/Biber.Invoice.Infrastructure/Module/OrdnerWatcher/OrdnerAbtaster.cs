using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.OrdnerWatcher;

/// <summary>
/// Tastet den Eingangsordner einmal ab und übernimmt fertig geschriebene Dateien in die Pipeline.
/// Fertig ist eine Datei, wenn ihre Größe seit <see cref="OrdnerEinstellungen.StabilSekunden"/> unverändert ist
/// und sie sich exklusiv öffnen lässt.
/// </summary>
public sealed class OrdnerAbtaster(
    IServiceScopeFactory scopes,
    IConfiguration konfiguration,
    TimeProvider zeit,
    ILogger<OrdnerAbtaster> log)
{
    public const string AbgelehntOrdner = "_abgelehnt";

    private static readonly HashSet<string> ErlaubteEndungen =
        new([".pdf", ".xml", ".png", ".jpg", ".jpeg", ".tif", ".tiff"], StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Beobachtung> _beobachtet = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _nichtLoeschbar = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _sperre = new(1, 1);

    public string Eingang => konfiguration["Pfade:Eingang"] ?? throw new InvalidOperationException("Pfade:Eingang fehlt.");

    /// <summary>Dateien direkt im Eingang, die übernommen werden könnten (ohne versteckte und temporäre).</summary>
    public static IEnumerable<FileInfo> Kandidaten(string eingang) =>
        new DirectoryInfo(eingang).EnumerateFiles("*", SearchOption.TopDirectoryOnly).Where(IstKandidat);

    /// <returns>Anzahl übernommener Dateien.</returns>
    public async Task<int> AbtastenAsync(CancellationToken ct)
    {
        await _sperre.WaitAsync(ct);
        try
        {
            return await AbtastenGesperrtAsync(ct);
        }
        finally
        {
            _sperre.Release();
        }
    }

    private async Task<int> AbtastenGesperrtAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var einstellungen = await scope.ServiceProvider.GetRequiredService<IEinstellungen>().LesenAsync<OrdnerEinstellungen>(ct);
        var import = scope.ServiceProvider.GetRequiredService<IImportDienst>();
        var stabil = TimeSpan.FromSeconds(einstellungen.StabilSekunden);
        var jetzt = zeit.GetUtcNow();

        var dateien = Kandidaten(Eingang).ToList();
        var vorhanden = dateien.Select(d => d.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var weg in _beobachtet.Keys.Where(p => !vorhanden.Contains(p)).ToList())
        {
            _beobachtet.Remove(weg);
        }

        _nichtLoeschbar.RemoveWhere(p => !vorhanden.Contains(p));

        var uebernommen = 0;
        foreach (var datei in dateien)
        {
            ct.ThrowIfCancellationRequested();
            if (_nichtLoeschbar.Contains(datei.FullName))
            {
                continue;
            }

            if (!_beobachtet.TryGetValue(datei.FullName, out var b) || b.Groesse != datei.Length)
            {
                _beobachtet[datei.FullName] = new Beobachtung(datei.Length, jetzt);
                continue;
            }

            if (jetzt - b.SeitWann < stabil)
            {
                continue;
            }

            if (await UebernehmenAsync(datei, import, ct))
            {
                uebernommen++;
            }
        }

        return uebernommen;
    }

    private async Task<bool> UebernehmenAsync(FileInfo datei, IImportDienst import, CancellationToken ct)
    {
        var zulaessig = ErlaubteEndungen.Contains(datei.Extension);
        FileStream strom;
        try
        {
            strom = new FileStream(datei.FullName, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        catch (IOException)
        {
            return false; // noch gesperrt, z. B. vom Scanner
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        try
        {
            if (zulaessig)
            {
                await using (strom)
                {
                    await import.AnnehmenAsync(strom, datei.Name, ImportQuelle.Ordner, new ImportHerkunft(Pfad: datei.Name), ct);
                }
            }
            else
            {
                await strom.DisposeAsync();
                var ziel = AbgelehntZiel(datei.Name);
                File.Move(datei.FullName, ziel);
                _beobachtet.Remove(datei.FullName);
                await using var abgelehnt = File.OpenRead(ziel);
                await import.AnnehmenAsync(abgelehnt, datei.Name, ImportQuelle.Ordner, new ImportHerkunft(Pfad: datei.Name), ct);
                log.LogInformation("Datei {Datei} aus dem Eingang abgelehnt und nach {Ordner} verschoben", datei.Name, AbgelehntOrdner);
                return true;
            }
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException)
        {
            await strom.DisposeAsync();
            log.LogWarning(fehler, "Übernahme von {Datei} aus dem Eingang fehlgeschlagen; nächster Versuch beim nächsten Lauf", datei.Name);
            return false;
        }

        _beobachtet.Remove(datei.FullName);
        try
        {
            File.Delete(datei.FullName);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            _nichtLoeschbar.Add(datei.FullName);
            log.LogWarning(fehler, "Übernommene Datei {Datei} ließ sich nicht aus dem Eingang löschen", datei.Name);
        }

        log.LogInformation("Datei {Datei} aus dem Eingang übernommen", datei.Name);
        return true;
    }

    private string AbgelehntZiel(string name)
    {
        var ordner = Path.Combine(Eingang, AbgelehntOrdner);
        Directory.CreateDirectory(ordner);
        var ziel = Path.Combine(ordner, name);
        if (File.Exists(ziel))
        {
            var stempel = zeit.GetUtcNow().ToString("yyyyMMdd-HHmmss-fff");
            ziel = Path.Combine(ordner, $"{Path.GetFileNameWithoutExtension(name)}_{stempel}{Path.GetExtension(name)}");
        }

        return ziel;
    }

    private static bool IstKandidat(FileInfo datei)
    {
        var name = datei.Name;
        if (name.StartsWith('.') || name.StartsWith('~') || (datei.Attributes & FileAttributes.Hidden) != 0)
        {
            return false;
        }

        var endung = datei.Extension;
        return !endung.Equals(".tmp", StringComparison.OrdinalIgnoreCase)
            && !endung.Equals(".part", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record Beobachtung(long Groesse, DateTimeOffset SeitWann);
}
