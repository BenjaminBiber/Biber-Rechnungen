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
/// Verschieben benennt nur um (ohne Überschreiben); Inhalt, Hash und Schreibschutz bleiben.
/// </summary>
public sealed class AblageSchreiber(BiberDbContext db, IConfiguration konfiguration, TimeProvider zeit) : IAblageSchreiber
{
    public async Task<BelegDatei> AblegenAsync(Beleg beleg, Stream inhalt, DateiRolle rolle, string endung, CancellationToken ct)
    {
        endung = endung.ToLowerInvariant();
        var wurzel = Wurzel();
        var relativ = await ZielpfadAsync(beleg, endung, ct);
        var basis = Basis(relativ, endung);

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

    public async Task<IReadOnlyList<AblageVerschiebung>> VerschiebenAsync(Beleg beleg, CancellationToken ct)
    {
        var wurzel = Wurzel();
        var dateien = await db.BelegDateien
            .Where(d => d.BelegId == beleg.Id && (d.Rolle == DateiRolle.Original || d.Rolle == DateiRolle.EingebettetesXml))
            .OrderBy(d => d.Rolle)
            .ToListAsync(ct);
        var verschoben = new List<AblageVerschiebung>();
        try
        {
            foreach (var datei in dateien)
            {
                var endung = Path.GetExtension(datei.Pfad).TrimStart('.').ToLowerInvariant();
                var relativ = await ZielpfadAsync(beleg, endung, ct);
                var basis = Basis(relativ, endung);
                var quelle = Absolut(wurzel, datei.Pfad);
                if (LiegtSchonDort(datei.Pfad, relativ, basis) || !File.Exists(quelle))
                {
                    continue;
                }

                for (var nummer = 1; ; nummer++)
                {
                    var kandidat = nummer == 1 ? relativ : $"{basis}_{nummer}{relativ[basis.Length..]}";
                    var ziel = Absolut(wurzel, kandidat);
                    Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
                    try
                    {
                        File.Move(quelle, ziel, overwrite: false);
                    }
                    catch (IOException) when (File.Exists(ziel))
                    {
                        continue;
                    }

                    verschoben.Add(new AblageVerschiebung(datei, datei.Pfad, kandidat));
                    datei.Pfad = kandidat;
                    break;
                }

                LeereOrdnerEntfernen(wurzel, Path.GetDirectoryName(quelle)!);
            }
        }
        catch
        {
            Zuruecknehmen(verschoben);
            throw;
        }

        return verschoben;
    }

    public void Zuruecknehmen(IReadOnlyList<AblageVerschiebung> verschiebungen)
    {
        var wurzel = Wurzel();
        foreach (var v in verschiebungen.Reverse())
        {
            try
            {
                var ziel = Absolut(wurzel, v.Von);
                Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
                var quelle = Absolut(wurzel, v.Nach);
                File.Move(quelle, ziel, overwrite: false);
                v.Datei.Pfad = v.Von;
                LeereOrdnerEntfernen(wurzel, Path.GetDirectoryName(quelle)!);
            }
            catch (IOException)
            {
                // Best effort: Die Datei bleibt am neuen Ort; die Integritätsprüfung meldet einen fehlenden Pfad.
            }
        }
    }

    public Stream Oeffnen(BelegDatei datei) =>
        new FileStream(AbsoluterPfad(datei), FileMode.Open, FileAccess.Read, FileShare.Read);

    public string AbsoluterPfad(BelegDatei datei) => Absolut(Wurzel(), datei.Pfad);

    /// <summary>Relativer Ablagepfad aus Rechnungsdatum (sonst Importtag), Korrespondent und Rechnungsnummer.</summary>
    private async Task<string> ZielpfadAsync(Beleg beleg, string endung, CancellationToken ct)
    {
        var datum = beleg.Rechnungsdatum ?? DateOnly.FromDateTime(beleg.ErstelltAm.UtcDateTime);
        var korrespondent = beleg.Korrespondent?.Name;
        if (korrespondent is null && beleg.KorrespondentId is { } korrespondentId)
        {
            korrespondent = await db.Korrespondenten.Where(k => k.Id == korrespondentId).Select(k => k.Name).FirstOrDefaultAsync(ct);
        }

        return AblagePfadBilder.Bilden(datum.Year, korrespondent, datum, beleg.Rechnungsnummer, endung);
    }

    private static string Basis(string relativ, string endung) =>
        endung.Length == 0 ? relativ : relativ[..^(endung.Length + 1)];

    /// <summary>Der Pfad ist schon der Zielpfad oder dessen Variante mit Suffix <c>_2</c>, <c>_3</c> ….</summary>
    private static bool LiegtSchonDort(string pfad, string relativ, string basis)
    {
        if (pfad == relativ)
        {
            return true;
        }

        var endung = relativ[basis.Length..];
        if (!pfad.StartsWith(basis + "_", StringComparison.Ordinal) || !pfad.EndsWith(endung, StringComparison.Ordinal))
        {
            return false;
        }

        var nummer = pfad[(basis.Length + 1)..^endung.Length];
        return nummer.Length > 0 && nummer.All(char.IsAsciiDigit);
    }

    /// <summary>Entfernt leer gewordene Ordner von <paramref name="ordner"/> aufwärts, nie die Ablage-Wurzel selbst.</summary>
    private static void LeereOrdnerEntfernen(string wurzel, string ordner)
    {
        var grenze = Path.TrimEndingDirectorySeparator(wurzel);
        var vergleich = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var aktuell = Path.TrimEndingDirectorySeparator(ordner);
        while (aktuell.Length > grenze.Length && aktuell.StartsWith(grenze, vergleich)
            && Directory.Exists(aktuell) && !Directory.EnumerateFileSystemEntries(aktuell).Any())
        {
            try
            {
                Directory.Delete(aktuell);
            }
            catch (IOException)
            {
                return;
            }

            aktuell = Path.GetDirectoryName(aktuell)!;
        }
    }

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
