using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Domain.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using ArtikelEntitaet = Biber.Invoice.Domain.Entitaeten.Artikel;

namespace Biber.Invoice.Infrastructure.Module.Positionen;

/// <summary>
/// Schritt 5: Normalisiert die Positionstexte, ordnet sie je Korrespondent einem Artikel zu (legt ihn sonst an)
/// und berechnet den Grundpreis. Ohne Korrespondent bleiben die Positionen ohne Artikel.
/// </summary>
public sealed class PositionenSchritt(BiberDbContext db) : IPipelineSchritt
{
    private const int MaxNameNormalisiert = 500;

    public int Reihenfolge => SchrittReihenfolge.Positionen;

    public string Name => "Positionen";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        var korrespondentId = beleg.KorrespondentId;
        Dictionary<string, ArtikelEntitaet> artikel = [];
        if (korrespondentId is not null)
        {
            artikel = await db.Artikel.Include(a => a.Stammartikel)
                .Where(a => a.KorrespondentId == korrespondentId)
                .ToDictionaryAsync(a => a.NameNormalisiert, ct);
        }

        foreach (var position in beleg.Positionen)
        {
            var normalisiert = PositionsNormalisierer.Normalisieren(position.Text);
            if (normalisiert.Length > MaxNameNormalisiert)
            {
                normalisiert = normalisiert[..MaxNameNormalisiert];
            }

            position.TextNormalisiert = normalisiert.Length == 0 ? null : normalisiert;
            ArtikelEntitaet? zugeordnet = null;
            if (korrespondentId is not null && normalisiert.Length > 0)
            {
                if (!artikel.TryGetValue(normalisiert, out zugeordnet))
                {
                    zugeordnet = new ArtikelEntitaet
                    {
                        Id = Guid.NewGuid(),
                        KorrespondentId = korrespondentId.Value,
                        Name = PositionsNormalisierer.Anzeigename(position.Text),
                        NameNormalisiert = normalisiert,
                        Einheit = string.IsNullOrWhiteSpace(position.Einheit) ? null : position.Einheit.Trim(),
                    };
                    db.Artikel.Add(zugeordnet);
                    artikel[normalisiert] = zugeordnet;
                }
            }

            position.ArtikelId = zugeordnet?.Id;
            position.Artikel = zugeordnet;

            var stamm = zugeordnet?.Stammartikel;
            var grundpreis = GrundpreisRechner.Berechnen(
                position.Text,
                position.Menge,
                position.Einheit,
                position.Einzelpreis,
                stamm is null ? null : zugeordnet!.UmrechnungFaktor,
                stamm?.GrundpreisMenge ?? 1,
                stamm?.Basiseinheit);
            position.Grundpreis = grundpreis?.Preis;
            position.GrundpreisEinheit = grundpreis?.Einheit;
        }

        return SchrittErgebnis.Weiter;
    }
}
