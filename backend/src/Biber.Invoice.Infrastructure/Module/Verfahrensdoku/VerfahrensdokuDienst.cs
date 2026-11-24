using System.Globalization;
using System.Text.RegularExpressions;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Microsoft.Extensions.Configuration;

namespace Biber.Invoice.Infrastructure.Module.Verfahrensdoku;

/// <summary>Software-Version der API (<c>AssemblyInformationalVersion</c>), von der Api beigesteuert.</summary>
public sealed record SoftwareVersion(string Wert);

/// <summary>
/// Liest <c>verfahrensdokumentation.md</c> aus dem Ordner <c>Verfahrensdoku:Pfad</c> und liefert Stand
/// und PDF. Fehlt die Datei, liefern die Methoden <c>null</c> (die Endpunkte antworten dann mit 404).
/// </summary>
public sealed partial class VerfahrensdokuDienst(IConfiguration konfiguration, SoftwareVersion softwareVersion)
{
    public const string Dateiname = "verfahrensdokumentation.md";
    private const string UnbekannteVersion = "unbekannt";
    private const string OffeneStelle = "[ERGÄNZEN";

    private static readonly string[] DatumFormate = ["yyyy-MM-dd", "d.M.yyyy"];

    public async Task<VerfahrensdokuDto?> LesenAsync(CancellationToken ct)
    {
        var dokument = await DokumentLesenAsync(ct);
        return dokument is null ? null : Info(dokument.Value.Text, dokument.Value.Geaendert);
    }

    public async Task<byte[]?> PdfAsync(CancellationToken ct)
    {
        var dokument = await DokumentLesenAsync(ct);
        if (dokument is null)
        {
            return null;
        }

        var info = Info(dokument.Value.Text, dokument.Value.Geaendert);
        return VerfahrensdokuPdf.Erzeugen(dokument.Value.Text, info.Version, softwareVersion.Wert);
    }

    private VerfahrensdokuDto Info(string text, DateOnly geaendert)
    {
        var (version, stand) = Kopfzeilen(text);
        return new VerfahrensdokuDto(
            version ?? UnbekannteVersion,
            stand ?? geaendert,
            OffeneStellen: Zaehlen(text, OffeneStelle),
            softwareVersion.Wert);
    }

    private async Task<(string Text, DateOnly Geaendert)?> DokumentLesenAsync(CancellationToken ct)
    {
        var ordner = konfiguration["Verfahrensdoku:Pfad"];
        if (string.IsNullOrWhiteSpace(ordner))
        {
            return null;
        }

        var datei = Path.Combine(ordner, Dateiname);
        if (!File.Exists(datei))
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(datei, ct);
        var geaendert = DateOnly.FromDateTime(File.GetLastWriteTimeUtc(datei));
        return (text, geaendert);
    }

    /// <summary>
    /// Zeilen der Form „Version: …“ und „Stand: …“ (auch in Zitat- oder Fettschrift); ersatzweise
    /// „Stand 2026-11-23“ im Text und die letzte Zeile der Versionstabelle.
    /// </summary>
    private static (string? Version, DateOnly? Stand) Kopfzeilen(string text)
    {
        string? version = null;
        DateOnly? stand = null;
        foreach (var zeile in text.Split('\n'))
        {
            var treffer = KopfzeileRegex().Match(zeile);
            if (!treffer.Success)
            {
                continue;
            }

            var wert = treffer.Groups["wert"].Value.Trim().Trim('*', '_', ' ');
            if (wert.Length == 0)
            {
                continue;
            }

            if (treffer.Groups["name"].Value.Equals("Version", StringComparison.OrdinalIgnoreCase))
            {
                version ??= wert;
            }
            else
            {
                stand ??= Datum(wert);
            }
        }

        stand ??= StandImText(text);
        version ??= LetzteVersionAusTabelle(text);
        return (version, stand);
    }

    private static DateOnly? StandImText(string text)
    {
        var treffer = StandImTextRegex().Match(text);
        return treffer.Success ? Datum(treffer.Groups["datum"].Value) : null;
    }

    private static string? LetzteVersionAusTabelle(string text)
    {
        string? letzte = null;
        var inTabelle = false;
        foreach (var zeile in text.Split('\n').Select(z => z.Trim()))
        {
            if (!zeile.StartsWith('|'))
            {
                inTabelle = false;
                continue;
            }

            var zellen = zeile.Trim('|').Split('|').Select(z => z.Trim()).ToArray();
            if (zellen[0].Equals("Version", StringComparison.OrdinalIgnoreCase))
            {
                inTabelle = true;
                letzte = null;
            }
            else if (inTabelle && zellen[0].Length > 0 && !zellen[0].StartsWith('-') && !zellen[0].StartsWith(':'))
            {
                letzte = zellen[0];
            }
        }

        return letzte;
    }

    private static DateOnly? Datum(string wert) =>
        DateOnly.TryParseExact(wert, DatumFormate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum)
            ? datum
            : null;

    private static int Zaehlen(string text, string suchtext)
    {
        var anzahl = 0;
        for (var index = text.IndexOf(suchtext, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(suchtext, index + suchtext.Length, StringComparison.Ordinal))
        {
            anzahl++;
        }

        return anzahl;
    }

    [GeneratedRegex(@"^[\s>*_-]*(?<name>Version|Stand)\s*:\s*(?<wert>.+?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex KopfzeileRegex();

    [GeneratedRegex(@"\bStand\s+(?<datum>\d{4}-\d{2}-\d{2}|\d{1,2}\.\d{1,2}\.\d{4})")]
    private static partial Regex StandImTextRegex();
}
