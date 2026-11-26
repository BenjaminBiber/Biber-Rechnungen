using System.ComponentModel;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Artikel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Biber.Invoice.Api.Module.Mcp;

/// <summary>Tools <c>artikel_suchen</c> und <c>artikel_preisverlauf</c>.</summary>
[McpServerToolType]
public sealed class ArtikelTools(IArtikelDienst artikel, TimeProvider zeit)
{
    public const int Seitengroesse = 20;

    [McpServerTool(Name = "artikel_suchen", ReadOnly = true)]
    [Description("Sucht gekaufte Artikel (je Lieferant) oder Stammartikel (lieferantenübergreifend) mit letztem Preis und Grundpreis.")]
    public async Task<string> ArtikelSuchenAsync(
        [Description("Suchbegriff im Artikelnamen.")] string suche,
        [Description("true: nur Stammartikel suchen, false: Artikel der Lieferanten.")] bool nur_stammartikel = false,
        CancellationToken ct = default)
    {
        if (nur_stammartikel)
        {
            var stamm = await artikel.StammartikelListeAsync(ct);
            return McpHilfe.Json(new
            {
                Stammartikel = stamm
                    .Where(s => string.IsNullOrWhiteSpace(suche) || s.Name.Contains(suche.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Take(Seitengroesse)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Basiseinheit,
                        s.GrundpreisMenge,
                        LetzterGrundpreis = s.Verlauf.Length == 0 ? (decimal?)null : s.Verlauf[^1],
                        s.GuenstigsterPreis,
                        s.GuenstigsterBei,
                        s.Korrespondenten,
                    }),
            });
        }

        var seite = await artikel.SuchenAsync(new ArtikelFilterDto { Suche = suche, Seitengroesse = Seitengroesse }, ct);
        return McpHilfe.Json(new
        {
            seite.Gesamt,
            Artikel = seite.Eintraege.Select(a => new
            {
                a.Id,
                a.Name,
                a.Artikelnummer,
                Korrespondent = a.Korrespondent.Name,
                Stammartikel = a.Stammartikel?.Name,
                LetzterPreis = a.LetzterEinzelpreis,
                a.Einheit,
                a.Grundpreis,
                a.GrundpreisEinheit,
                a.ZuletztGekauft,
                a.Kaeufe,
            }),
        });
    }

    [McpServerTool(Name = "artikel_preisverlauf", ReadOnly = true)]
    [Description("Liefert den Preisverlauf eines Artikels oder Stammartikels je Korrespondent für ein Jahr.")]
    public async Task<string> ArtikelPreisverlaufAsync(
        [Description("ID (GUID) des Artikels oder Stammartikels.")] Guid id,
        [Description("Jahr; ohne Angabe das laufende Jahr.")] int? jahr = null,
        CancellationToken ct = default)
    {
        int j = jahr ?? zeit.GetUtcNow().Year;
        var detail = await artikel.ArtikelDetailAsync(id, j, ct)
            ?? await artikel.StammartikelDetailAsync(id, j, ct)
            ?? throw new McpException($"Artikel oder Stammartikel {id} nicht gefunden.");

        return McpHilfe.Json(new
        {
            detail.Id,
            detail.Name,
            detail.IstStammartikel,
            detail.Basiseinheit,
            detail.GrundpreisMenge,
            Jahr = j,
            detail.Kennzahlen.GuenstigsterGrundpreis,
            detail.Kennzahlen.GuenstigsterBei,
            Serien = detail.Serien.Select(s => new
            {
                Korrespondent = s.Korrespondent.Name,
                s.Einheit,
                Punkte = s.Punkte.Select(p => new { p.Datum, p.Einzelpreis, p.Grundpreis }),
            }),
        });
    }
}
