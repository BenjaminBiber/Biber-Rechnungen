using System.Globalization;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.HashDublette;

/// <summary>
/// Schritt 1: Erkennt identische Dateien über den SHA-256. Ein Treffer verknüpft den neuen Beleg mit dem
/// ältesten Beleg derselben Datei, übernimmt dessen Kopfdaten, löscht die Staging-Datei und beendet die Pipeline.
/// </summary>
public sealed class HashDubletteSchritt(BiberDbContext db, IStagingSpeicher staging) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.HashDublette;

    public string Name => "Dubletten-Check";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        if (kontext.IstWiederholung || string.IsNullOrEmpty(kontext.Vorgang.Sha256))
        {
            return SchrittErgebnis.Weiter;
        }

        var sha256 = kontext.Vorgang.Sha256;
        var belegId = kontext.Beleg.Id;
        var treffer = await db.Belege
            .Where(b => b.Id != belegId
                && db.BelegDateien.Any(d => d.BelegId == b.Id && d.Rolle == DateiRolle.Original && d.Sha256 == sha256))
            .OrderBy(b => b.ErstelltAm)
            .ThenBy(b => b.Id)
            .FirstOrDefaultAsync(ct);
        if (treffer is null)
        {
            return SchrittErgebnis.Weiter;
        }

        // Bei Ketten auf den ältesten Beleg zeigen: ein Beleg, der selbst Dublette ist, wird nie Ziel.
        var originalId = treffer.DuplikatVonId ?? treffer.Id;
        var original = originalId == treffer.Id ? treffer : await db.Belege.SingleAsync(b => b.Id == originalId, ct);

        var beleg = kontext.Beleg;
        beleg.DuplikatVonId = original.Id;
        beleg.Status = BelegStatus.Fertig;
        beleg.Belegart = original.Belegart;
        beleg.KorrespondentId = original.KorrespondentId;
        beleg.KorrespondentErkennung = original.KorrespondentErkennung;
        beleg.Rechnungsnummer = original.Rechnungsnummer;
        beleg.Rechnungsdatum = original.Rechnungsdatum;
        beleg.Waehrung = original.Waehrung;
        beleg.Netto = original.Netto;
        beleg.Steuer = original.Steuer;
        beleg.Brutto = original.Brutto;

        staging.Loeschen(kontext.DateiPfad);
        kontext.Vorgang.StagingPfad = null;
        kontext.Vorgang.BelegId = beleg.Id;

        return SchrittErgebnis.Beendet(ImportStatus.Dublette, Text(original));
    }

    private static string Text(Beleg original)
    {
        var nummer = string.IsNullOrWhiteSpace(original.Rechnungsnummer) ? "ohne Rechnungsnummer" : original.Rechnungsnummer;
        var datum = original.Rechnungsdatum is { } d ? $" vom {d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}" : "";
        return $"Identische Datei (SHA-256) wie Beleg {nummer}{datum} – als Dublette verknüpft, nicht doppelt gezählt.";
    }
}
