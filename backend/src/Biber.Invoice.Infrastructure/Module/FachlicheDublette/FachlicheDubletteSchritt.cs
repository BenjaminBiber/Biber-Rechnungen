using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.FachlicheDublette.Domain.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.FachlicheDublette;

/// <summary>
/// Schritt 6: Erkennt fachlich doppelte Belege – gleicher Korrespondent, gleiche (normalisierte) Rechnungsnummer,
/// gleicher Bruttobetrag – und verknüpft sie über <c>DuplikatVonId</c> mit dem zuerst erstellten Beleg. Die Dublette
/// läuft weiter bis in die Ablage, weil ihre Datei eine andere ist. Ohne Treffer wird eine frühere Verknüpfung gelöst.
/// </summary>
public sealed class FachlicheDubletteSchritt(BiberDbContext db) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.FachlicheDublette;

    public string Name => "Fachliche Dublette";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        var nummer = RechnungsnummerNormalisierer.Normalisieren(beleg.Rechnungsnummer);
        if (beleg.KorrespondentId is not { } korrespondentId || nummer is null || beleg.Brutto is not { } brutto)
        {
            beleg.DuplikatVonId = null;
            return SchrittErgebnis.Weiter;
        }

        // Nur die Belege des Korrespondenten mit gleichem Betrag laden; die Nummern werden erst im Speicher verglichen.
        var kandidaten = await db.Belege
            .Where(b => b.Id != beleg.Id
                && b.KorrespondentId == korrespondentId
                && b.Brutto == brutto
                && b.DuplikatVonId == null
                && b.Rechnungsnummer != null)
            .Select(b => new { b.Id, b.Rechnungsnummer, b.ErstelltAm })
            .ToListAsync(ct);

        var original = kandidaten
            .Where(k => RechnungsnummerNormalisierer.Normalisieren(k.Rechnungsnummer) == nummer)
            .OrderBy(k => k.ErstelltAm)
            .ThenBy(k => k.Id)
            .FirstOrDefault();

        // Der zuerst erstellte Beleg ist das Original; ist der aktuelle der ältere, bleibt er unverknüpft.
        var istDublette = original is not null
            && (original.ErstelltAm < beleg.ErstelltAm
                || (original.ErstelltAm == beleg.ErstelltAm && original.Id.CompareTo(beleg.Id) < 0));
        beleg.DuplikatVonId = istDublette ? original!.Id : null;
        return SchrittErgebnis.Weiter;
    }
}
