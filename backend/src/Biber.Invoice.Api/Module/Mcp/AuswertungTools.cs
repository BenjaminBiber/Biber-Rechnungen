using System.ComponentModel;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Auswertung;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Biber.Invoice.Api.Module.Mcp;

/// <summary>Tool <c>kosten_auswerten</c>.</summary>
[McpServerToolType]
public sealed class AuswertungTools(IAuswertungsDienst auswertung)
{
    [McpServerTool(Name = "kosten_auswerten", ReadOnly = true)]
    [Description("Wertet die Kosten (Eingangsrechnungen) eines Zeitraums nach Rechnungsdatum aus, gruppiert nach Monat, Kategorie oder Korrespondent.")]
    public async Task<string> KostenAuswertenAsync(
        [Description("Rechnungsdatum ab (einschließlich), ISO-Datum JJJJ-MM-TT.")] DateOnly von,
        [Description("Rechnungsdatum bis (einschließlich), ISO-Datum JJJJ-MM-TT.")] DateOnly bis,
        [Description("Gruppierung: monat, kategorie oder korrespondent.")] string gruppierung,
        CancellationToken ct = default)
    {
        if (bis < von)
        {
            throw new McpException("„bis“ liegt vor „von“.");
        }

        var art = gruppierung?.Trim().ToLowerInvariant() switch
        {
            "monat" => KostenGruppierung.Monat,
            "kategorie" => KostenGruppierung.Kategorie,
            "korrespondent" => KostenGruppierung.Korrespondent,
            _ => throw new McpException($"Unbekannte Gruppierung „{gruppierung}“. Erlaubt: monat, kategorie, korrespondent."),
        };

        var ergebnis = await auswertung.KostenAsync(von, bis, art, ct);
        return McpHilfe.Json(new
        {
            Von = von,
            Bis = bis,
            Gruppierung = art,
            ergebnis.SummeBrutto,
            Zeilen = ergebnis.Zeilen.Select(z => new { z.Schluessel, z.Bezeichnung, z.Netto, z.Steuer, z.Brutto, z.AnzahlBelege }),
        });
    }
}
