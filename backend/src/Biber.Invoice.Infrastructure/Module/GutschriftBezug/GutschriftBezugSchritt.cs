using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.GutschriftBezug.Domain.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.GutschriftBezug;

/// <summary>
/// Schritt 6b: Verknüpft eine Gutschrift mit der Rechnung desselben Korrespondenten, auf die sie sich bezieht. Eine
/// vorhandene (auch manuelle) Verknüpfung bleibt stehen; ohne eindeutigen Treffer bleibt die Gutschrift unverknüpft.
/// </summary>
public sealed class GutschriftBezugSchritt(BiberDbContext db) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.GutschriftBezug;

    public string Name => "Gutschrift zuordnen";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        if (beleg.Belegart != Belegart.Gutschrift || beleg.BezugBelegId is not null || beleg.KorrespondentId is not { } korrespondentId)
        {
            return SchrittErgebnis.Weiter;
        }

        var kandidaten = await db.Belege
            .AsNoTracking()
            .Where(b => b.Id != beleg.Id
                && b.KorrespondentId == korrespondentId
                && b.Belegart == Belegart.Rechnung
                && b.DuplikatVonId == null
                && b.Rechnungsnummer != null)
            .Select(b => new RechnungKandidat(b.Id, b.Rechnungsnummer!))
            .ToListAsync(ct);

        beleg.BezugBelegId = GutschriftBezugFinder.Finden(
            kontext.Extraktion?.BezugRechnungsnummer, beleg.Volltext, beleg.Rechnungsnummer, kandidaten);
        return SchrittErgebnis.Weiter;
    }
}
