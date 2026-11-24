using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Biber.Invoice.Infrastructure.Module.Belege.Lesen;

/// <summary>Eine auslieferbare Datei: absoluter Pfad, MIME-Typ und Dateiname für <c>Content-Disposition</c>.</summary>
public sealed record AusgelieferteDatei(string Pfad, string MimeType, string Dateiname);

/// <summary>
/// Findet die Datei eines Belegs auf der Platte. Original und eingebettetes XML liegen unter <c>Pfade:Ablage</c>,
/// Visualisierung und Vorschau unter <c>Pfade:Daten</c>. Pfade, die die Wurzel verlassen, gelten als nicht vorhanden.
/// </summary>
public sealed class BelegDateiQuelle(BiberDbContext db, IConfiguration konfiguration)
{
    /// <returns><c>null</c>, wenn Beleg, Datei oder die Datei auf der Platte fehlen.</returns>
    public async Task<AusgelieferteDatei?> DateiAsync(Guid belegId, Guid dateiId, CancellationToken ct)
    {
        var datei = await db.BelegDateien
            .AsNoTracking()
            .Where(d => d.Id == dateiId && d.BelegId == belegId)
            .Select(d => new { d.Rolle, d.Pfad, d.MimeType })
            .FirstOrDefaultAsync(ct);
        return datei is null ? null : Aufloesen(datei.Rolle, datei.Pfad, datei.MimeType);
    }

    /// <returns>Die HTML-Visualisierung des Belegs oder <c>null</c>, wenn es keine gibt.</returns>
    public async Task<AusgelieferteDatei?> VisualisierungAsync(Guid belegId, CancellationToken ct)
    {
        var datei = await db.BelegDateien
            .AsNoTracking()
            .Where(d => d.BelegId == belegId && d.Rolle == DateiRolle.Visualisierung)
            .OrderByDescending(d => d.AbgelegtAm)
            .Select(d => new { d.Rolle, d.Pfad, d.MimeType })
            .FirstOrDefaultAsync(ct);
        return datei is null ? null : Aufloesen(datei.Rolle, datei.Pfad, "text/html");
    }

    private AusgelieferteDatei? Aufloesen(DateiRolle rolle, string relativ, string mimeType)
    {
        var schluessel = rolle is DateiRolle.Original or DateiRolle.EingebettetesXml ? "Pfade:Ablage" : "Pfade:Daten";
        if (konfiguration[schluessel] is not { Length: > 0 } wurzelText || string.IsNullOrWhiteSpace(relativ))
        {
            return null;
        }

        var wurzel = Path.GetFullPath(wurzelText);
        var absolut = Path.GetFullPath(Path.Combine(wurzel, relativ));
        var praefix = Path.EndsInDirectorySeparator(wurzel) ? wurzel : wurzel + Path.DirectorySeparatorChar;
        var vergleich = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!absolut.StartsWith(praefix, vergleich) || !File.Exists(absolut))
        {
            return null;
        }

        return new AusgelieferteDatei(
            absolut,
            string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType,
            Path.GetFileName(absolut));
    }
}
