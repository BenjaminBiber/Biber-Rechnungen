using Biber.Invoice.Contracts.Dto.Sonstiges;
using Microsoft.Extensions.Configuration;

namespace Biber.Invoice.Infrastructure.Module.Integritaet;

/// <summary>Liest den Stand der Datenbank-Dumps (<c>*.dump</c>, <c>*.sql.gz</c>) aus <c>Pfade:Backup</c>, nur lesend.</summary>
public sealed class BackupStatusLeser(IConfiguration konfiguration)
{
    public BackupStatusDto Lesen()
    {
        var ordner = konfiguration["Pfade:Backup"];
        var dumps = new List<FileInfo>();
        try
        {
            if (!string.IsNullOrEmpty(ordner) && Directory.Exists(ordner))
            {
                var verzeichnis = new DirectoryInfo(ordner);
                dumps.AddRange(verzeichnis.EnumerateFiles("*.dump"));
                dumps.AddRange(verzeichnis.EnumerateFiles("*.sql.gz"));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            dumps.Clear();
        }

        var juengster = dumps.MaxBy(d => d.LastWriteTimeUtc);
        return juengster is null
            ? new BackupStatusDto(null, null, 0)
            : new BackupStatusDto(new DateTimeOffset(juengster.LastWriteTimeUtc, TimeSpan.Zero), juengster.Length, dumps.Count);
    }
}
