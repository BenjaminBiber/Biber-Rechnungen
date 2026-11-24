using System.Text.Json;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Logos;

/// <summary>
/// Nachts: holt für Korrespondenten mit Domain und ohne Logo das Favicon, höchstens <see cref="MaxJeLauf"/> je Lauf.
/// Jede Domain wird höchstens einmal in <see cref="Wartezeit"/> versucht; die Zeitpunkte stehen in der Einstellung
/// <see cref="EinstellungSchluessel"/>.
/// </summary>
public sealed class FaviconJob(
    BiberDbContext db,
    ILogoDienst logos,
    IFaviconAbruf favicons,
    TimeProvider zeit,
    ILogger<FaviconJob> log) : IWiederkehrenderJob
{
    public const string EinstellungSchluessel = "favicon.versuche";
    public const int MaxJeLauf = 20;
    public static readonly TimeSpan Wartezeit = TimeSpan.FromDays(30);

    public string Id => "favicons";

    public string Cron => "15 4 * * *";

    public async Task AusfuehrenAsync(CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var einstellung = await db.Einstellungen.FirstOrDefaultAsync(e => e.Schluessel == EinstellungSchluessel, ct);
        var versuche = Lesen(einstellung?.Wert);

        var kandidaten = await db.Korrespondenten
            .Where(k => k.Domain != null && k.Domain != "" && k.LogoQuelle == LogoQuelle.Keine && k.LogoPfad == null)
            .OrderBy(k => k.Name)
            .ToListAsync(ct);
        var faellig = kandidaten
            .Where(k => !versuche.TryGetValue(Schluessel(k.Domain!), out var zuletzt) || jetzt - zuletzt >= Wartezeit)
            .Take(MaxJeLauf)
            .ToList();

        var gesetzt = 0;
        foreach (var korrespondent in faellig)
        {
            versuche[Schluessel(korrespondent.Domain!)] = jetzt;
            var bild = await favicons.AbrufenAsync(korrespondent.Domain!, ct);
            if (bild is not null && await logos.SetzenAsync(korrespondent, bild, LogoQuelle.Favicon, erzwingen: false, ct))
            {
                gesetzt++;
            }
        }

        // Alte Versuche fallen heraus, damit die Einstellung nicht unbegrenzt wächst.
        foreach (var alt in versuche.Where(v => jetzt - v.Value >= Wartezeit).Select(v => v.Key).ToList())
        {
            versuche.Remove(alt);
        }

        if (einstellung is null)
        {
            einstellung = new Einstellung { Schluessel = EinstellungSchluessel };
            db.Einstellungen.Add(einstellung);
        }

        einstellung.Wert = JsonSerializer.Serialize(versuche);
        einstellung.GeaendertAm = jetzt;
        await db.SaveChangesAsync(ct);
        log.LogInformation("Favicons: {Versucht} versucht, {Gesetzt} gesetzt", faellig.Count, gesetzt);
    }

    private static string Schluessel(string domain) => domain.Trim().ToLowerInvariant();

    private static Dictionary<string, DateTimeOffset> Lesen(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert) || wert == "null")
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(wert) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
