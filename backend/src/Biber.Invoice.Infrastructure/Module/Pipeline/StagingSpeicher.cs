using System.Security.Cryptography;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>Legt angenommene Dateien unter <c>Pfade:Staging/{yyyyMMdd}/{Guid}{Endung}</c> ab.</summary>
public sealed class StagingSpeicher(IConfiguration konfiguration, TimeProvider zeit) : IStagingSpeicher
{
    public async Task<StagingDatei> SpeichernAsync(Stream inhalt, string dateiname, CancellationToken ct)
    {
        var basis = konfiguration["Pfade:Staging"]
            ?? throw new InvalidOperationException("Pfade:Staging ist nicht konfiguriert.");
        var ordner = Path.GetFullPath(Path.Combine(basis, zeit.GetUtcNow().ToString("yyyyMMdd")));
        Directory.CreateDirectory(ordner);
        var pfad = Path.Combine(ordner, Guid.NewGuid() + Path.GetExtension(dateiname).ToLowerInvariant());

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var puffer = new byte[81920];
        long groesse = 0;
        await using (var ziel = new FileStream(pfad, FileMode.CreateNew, FileAccess.Write, FileShare.None, puffer.Length, useAsync: true))
        {
            int gelesen;
            while ((gelesen = await inhalt.ReadAsync(puffer, ct)) > 0)
            {
                hash.AppendData(puffer, 0, gelesen);
                await ziel.WriteAsync(puffer.AsMemory(0, gelesen), ct);
                groesse += gelesen;
            }
        }

        return new StagingDatei(pfad, Convert.ToHexStringLower(hash.GetHashAndReset()), groesse);
    }

    public Stream Oeffnen(string pfad) => new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.Read);

    public void Loeschen(string pfad)
    {
        if (File.Exists(pfad))
        {
            File.Delete(pfad);
        }
    }
}
