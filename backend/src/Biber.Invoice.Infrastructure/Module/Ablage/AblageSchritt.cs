using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Ablage;

/// <summary>
/// Schritt 9: Legt das Original genau einmal in der WORM-Ablage ab (bei ZUGFeRD zusätzlich das eingebettete XML),
/// prüft den SHA-256 gegen den Import und löscht die Staging-Datei. Ein bereits abgelegtes Original bleibt unverändert (R2-6).
/// </summary>
public sealed class AblageSchritt(BiberDbContext db, IAblageSchreiber schreiber, IStagingSpeicher staging) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.Ablage;

    public string Name => "Ablage";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        if (kontext.IstWiederholung
            || beleg.Dateien.Any(d => d.Rolle == DateiRolle.Original)
            || await db.BelegDateien.AnyAsync(d => d.BelegId == beleg.Id && d.Rolle == DateiRolle.Original, ct))
        {
            return SchrittErgebnis.Weiter;
        }

        var endung = await EndungAsync(kontext, ct);
        BelegDatei original;
        await using (var inhalt = staging.Oeffnen(kontext.DateiPfad))
        {
            original = await schreiber.AblegenAsync(beleg, inhalt, DateiRolle.Original, endung, ct);
        }

        if (kontext.Vorgang.Sha256 is { } erwartet && !string.Equals(original.Sha256, erwartet, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SHA-256 der abgelegten Datei „{original.Pfad}“ weicht vom Import ab ({original.Sha256} statt {erwartet}).");
        }

        if (kontext.Format == ErkanntesFormat.ZugferdPdf && kontext.ERechnungXml is { } xml)
        {
            using var xmlInhalt = new MemoryStream(xml, writable: false);
            await schreiber.AblegenAsync(beleg, xmlInhalt, DateiRolle.EingebettetesXml, "xml", ct);
        }

        staging.Loeschen(kontext.DateiPfad);
        kontext.Vorgang.StagingPfad = null;
        return SchrittErgebnis.Weiter;
    }

    private async Task<string> EndungAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        switch (kontext.Format)
        {
            case ErkanntesFormat.Pdf or ErkanntesFormat.ZugferdPdf:
                return "pdf";
            case ErkanntesFormat.XRechnungXml:
                return "xml";
        }

        var kopf = new byte[4];
        await using (var strom = staging.Oeffnen(kontext.DateiPfad))
        {
            await strom.ReadAtLeastAsync(kopf, kopf.Length, throwOnEndOfStream: false, ct);
        }

        return kopf switch
        {
            [0x89, 0x50, 0x4E, 0x47] => "png",
            [0xFF, 0xD8, 0xFF, _] => "jpg",
            [0x49, 0x49, 0x2A, 0x00] or [0x4D, 0x4D, 0x00, 0x2A] => "tiff",
            [0x25, 0x50, 0x44, 0x46] => "pdf",
            _ => Path.GetExtension(kontext.Dateiname).TrimStart('.').ToLowerInvariant() is { Length: > 0 } e ? e : "bin",
        };
    }
}
