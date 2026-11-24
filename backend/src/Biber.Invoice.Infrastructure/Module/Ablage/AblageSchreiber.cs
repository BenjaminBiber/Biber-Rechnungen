using System.Security.Cryptography;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Domain.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Biber.Invoice.Infrastructure.Module.Ablage;

/// <summary>
/// Legt Dateien unter <c>Pfade:Ablage/{Jahr}/{Korrespondent}/{Datum}_{Rechnungsnr}.{ext}</c> ab. Schreibt mit
/// <see cref="FileMode.CreateNew"/>, hängt bei belegtem Pfad <c>_2</c>, <c>_3</c> … an und setzt die Datei danach schreibgeschützt.
/// </summary>
public sealed class AblageSchreiber(BiberDbContext db, IConfiguration konfiguration, TimeProvider zeit) : IAblageSchreiber
{
    public async Task<BelegDatei> AblegenAsync(Beleg beleg, Stream inhalt, DateiRolle rolle, string endung, CancellationToken ct)
    {
        endung = endung.ToLowerInvariant();
        var wurzel = Wurzel();
        var datum = beleg.Rechnungsdatum ?? DateOnly.FromDateTime(beleg.ErstelltAm.UtcDateTime);
        var korrespondent = beleg.Korrespondent?.Name;
        if (korrespondent is null && beleg.KorrespondentId is { } korrespondentId)
        {
            korrespondent = await db.Korrespondenten.Where(k => k.Id == korrespondentId).Select(k => k.Name).FirstOrDefaultAsync(ct);
        }

        var relativ = AblagePfadBilder.Bilden(datum.Year, korrespondent, datum, beleg.Rechnungsnummer, endung);
        var basis = relativ[..^(endung.Length + 1)];

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var puffer = new byte[81920];
        long groesse = 0;
        for (var nummer = 1; ; nummer++)
        {
            var kandidat = nummer == 1 ? relativ : $"{basis}_{nummer}{relativ[basis.Length..]}";
            var absolut = Absolut(wurzel, kandidat);
            Directory.CreateDirectory(Path.GetDirectoryName(absolut)!);
            FileStream ziel;
            try
            {
                ziel = new FileStream(absolut, FileMode.CreateNew, FileAccess.Write, FileShare.None, puffer.Length, useAsync: true);
            }
            catch (IOException) when (File.Exists(absolut))
            {
                continue;
            }

            await using (ziel)
            {
                int gelesen;
                while ((gelesen = await inhalt.ReadAsync(puffer, ct)) > 0)
                {
                    hash.AppendData(puffer, 0, gelesen);
                    await ziel.WriteAsync(puffer.AsMemory(0, gelesen), ct);
                    groesse += gelesen;
                }
            }

            Schreibschuetzen(absolut);
            var datei = new BelegDatei
            {
                Id = Guid.NewGuid(),
                BelegId = beleg.Id,
                Rolle = rolle,
                Pfad = kandidat,
                Sha256 = Convert.ToHexStringLower(hash.GetHashAndReset()),
                Groesse = groesse,
                MimeType = MimeType(endung),
                AbgelegtAm = zeit.GetUtcNow(),
            };
            db.BelegDateien.Add(datei);
            if (!beleg.Dateien.Contains(datei))
            {
                beleg.Dateien.Add(datei);
            }

            return datei;
        }
    }

    public Stream Oeffnen(BelegDatei datei) =>
        new FileStream(AbsoluterPfad(datei), FileMode.Open, FileAccess.Read, FileShare.Read);

    public string AbsoluterPfad(BelegDatei datei) => Absolut(Wurzel(), datei.Pfad);

    private string Wurzel() => Path.GetFullPath(konfiguration["Pfade:Ablage"]
        ?? throw new InvalidOperationException("Pfade:Ablage ist nicht konfiguriert."));

    /// <summary>Löst den relativen Pfad auf und stellt sicher, dass er die Ablage-Wurzel nicht verlässt.</summary>
    private static string Absolut(string wurzel, string relativ)
    {
        var absolut = Path.GetFullPath(Path.Combine(wurzel, relativ));
        var praefix = Path.EndsInDirectorySeparator(wurzel) ? wurzel : wurzel + Path.DirectorySeparatorChar;
        if (!absolut.StartsWith(praefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Ablagepfad „{relativ}“ liegt außerhalb der Ablage.");
        }

        return absolut;
    }

    private static void Schreibschuetzen(string pfad)
    {
        File.SetAttributes(pfad, File.GetAttributes(pfad) | FileAttributes.ReadOnly);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(pfad, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
    }

    private static string MimeType(string endung) => endung switch
    {
        "pdf" => "application/pdf",
        "xml" => "application/xml",
        "png" => "image/png",
        "jpg" => "image/jpeg",
        "tiff" => "image/tiff",
        _ => "application/octet-stream",
    };
}
