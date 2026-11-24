using System.ComponentModel;
using Biber.Invoice.Contracts.Dienste;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Biber.Invoice.Api.Module.Mcp;

/// <summary>Tool <c>korrespondent_abrufen</c>.</summary>
[McpServerToolType]
public sealed class KorrespondentTools(IKorrespondentDienst korrespondenten, IConfiguration konfiguration, TimeProvider zeit)
{
    [McpServerTool(Name = "korrespondent_abrufen", ReadOnly = true)]
    [Description("Ruft einen Korrespondenten (Lieferanten) ab: Stammdaten, Kosten des laufenden Jahres, Anzahl Belege und die fünf letzten Belege.")]
    public async Task<string> KorrespondentAbrufenAsync(
        [Description("Name oder ID (GUID) des Korrespondenten.")] string name_oder_id,
        CancellationToken ct = default)
    {
        int jahr = zeit.GetUtcNow().Year;
        if (!Guid.TryParse(name_oder_id, out var id))
        {
            var alle = await korrespondenten.ListeAsync(null, "Alle", jahr, ct);
            id = McpHilfe.Aufloesen("Korrespondent", name_oder_id ?? "", alle.Select(k => (k.Id, k.Name)));
        }

        var k = await korrespondenten.AbrufenAsync(id, jahr, ct)
            ?? throw new McpException($"Korrespondent {id} nicht gefunden.");

        return McpHilfe.Json(new
        {
            k.Id,
            k.Name,
            k.Aliase,
            k.Anschrift,
            k.UstIdNr,
            k.Steuernummer,
            k.Ibans,
            k.Website,
            k.Email,
            k.IstLieferant,
            k.IstKunde,
            DefaultKategorie = k.DefaultKategorie?.Name,
            Jahr = jahr,
            KostenJahr = k.Kennzahlen.KostenJahr,
            AnzahlBelege = k.Kennzahlen.AnzahlBelege,
            OffenAnzahl = k.Kennzahlen.OffenAnzahl,
            OffenSumme = k.Kennzahlen.OffenSumme,
            LetzteBelege = k.LetzteBelege.Take(5).Select(b => new
            {
                b.Id,
                b.Rechnungsnummer,
                Datum = b.Rechnungsdatum,
                Faelligkeit = b.Faelligkeitsdatum,
                b.Brutto,
                Link = McpHilfe.BelegLink(konfiguration, b.Id),
            }),
        });
    }
}
