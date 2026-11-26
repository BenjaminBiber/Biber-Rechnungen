using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Domain.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using ArtikelEntitaet = Biber.Invoice.Domain.Entitaeten.Artikel;
using PositionEntitaet = Biber.Invoice.Domain.Entitaeten.Position;

namespace Biber.Invoice.Infrastructure.Module.Positionen;

/// <summary>
/// Schritt 5: Normalisiert die Positionstexte, ordnet sie je Korrespondent einem Artikel zu (legt ihn sonst an)
/// und berechnet den Grundpreis. Ohne Korrespondent bleiben die Positionen ohne Artikel.
/// </summary>
/// <remarks>
/// Mit Artikelnummer entscheidet allein die Nummer: Größen derselben Nummer sind ein Artikel, gleichnamige Positionen
/// mit verschiedenen Nummern verschiedene. Ohne Nummer zählt der normalisierte Text; passt er zu keinem Artikel ohne
/// Nummer, aber zu genau einem mit Nummer, wird dieser genommen (die Nummer wurde nur nicht erkannt).
/// </remarks>
public sealed class PositionenSchritt(BiberDbContext db) : IPipelineSchritt
{
    private const int MaxNameNormalisiert = 500;

    public int Reihenfolge => SchrittReihenfolge.Positionen;

    public string Name => "Positionen";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        var korrespondentId = beleg.KorrespondentId;
        var katalog = korrespondentId is null
            ? null
            : new Katalog(await db.Artikel.Include(a => a.Stammartikel)
                .Where(a => a.KorrespondentId == korrespondentId)
                .ToListAsync(ct));

        foreach (var position in beleg.Positionen)
        {
            var normalisiert = PositionsNormalisierer.Normalisieren(position.Text);
            if (normalisiert.Length > MaxNameNormalisiert)
            {
                normalisiert = normalisiert[..MaxNameNormalisiert];
            }

            position.TextNormalisiert = normalisiert.Length == 0 ? null : normalisiert;
            var nummer = ArtikelnummerNormalisierer.Normalisieren(position.Artikelnummer);
            ArtikelEntitaet? zugeordnet = null;
            if (katalog is not null && (nummer is not null || normalisiert.Length > 0))
            {
                zugeordnet = katalog.Finden(nummer, normalisiert);
                if (zugeordnet is null)
                {
                    zugeordnet = Anlegen(korrespondentId!.Value, position, nummer, normalisiert, katalog.OhneNummer(normalisiert));
                    db.Artikel.Add(zugeordnet);
                    katalog.Hinzufuegen(zugeordnet);
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

    /// <summary>
    /// Neuer Artikel; ohne Text heißt er wie seine Nummer. Ein gleichnamiger Artikel ohne Nummer (<paramref name="vorlage"/>,
    /// z. B. von vor dem Auslesen der Nummern) gibt Stammartikel, Umrechnung und Einheit weiter.
    /// </summary>
    private static ArtikelEntitaet Anlegen(
        Guid korrespondentId,
        PositionEntitaet position,
        string? nummer,
        string normalisiert,
        ArtikelEntitaet? vorlage)
    {
        var name = PositionsNormalisierer.Anzeigename(position.Text);
        var einheit = string.IsNullOrWhiteSpace(position.Einheit) ? null : position.Einheit.Trim();
        var artikel = new ArtikelEntitaet
        {
            Id = Guid.NewGuid(),
            KorrespondentId = korrespondentId,
            Artikelnummer = nummer,
            Name = name.Length > 0 ? name : nummer!,
            NameNormalisiert = normalisiert.Length > 0 ? normalisiert : nummer!.ToLowerInvariant(),
            Einheit = einheit,
        };
        if (nummer is not null && vorlage?.StammartikelId is not null)
        {
            artikel.StammartikelId = vorlage.StammartikelId;
            artikel.Stammartikel = vorlage.Stammartikel;
            artikel.UmrechnungFaktor = vorlage.UmrechnungFaktor;
            artikel.Einheit ??= vorlage.Einheit;
        }

        return artikel;
    }

    /// <summary>Artikel eines Korrespondenten, nachgeschlagen über Nummer bzw. normalisierten Namen.</summary>
    private sealed class Katalog
    {
        private readonly Dictionary<string, ArtikelEntitaet> _nachNummer = [];
        private readonly Dictionary<string, ArtikelEntitaet> _ohneNummer = [];
        private readonly Dictionary<string, List<ArtikelEntitaet>> _mitNummerNachName = [];

        public Katalog(IEnumerable<ArtikelEntitaet> artikel)
        {
            foreach (var a in artikel)
            {
                Hinzufuegen(a);
            }
        }

        public ArtikelEntitaet? Finden(string? nummer, string normalisiert)
        {
            if (nummer is not null)
            {
                return _nachNummer.GetValueOrDefault(nummer);
            }

            if (_ohneNummer.TryGetValue(normalisiert, out var artikel))
            {
                return artikel;
            }

            return _mitNummerNachName.TryGetValue(normalisiert, out var gleichnamig) && gleichnamig.Count == 1
                ? gleichnamig[0]
                : null;
        }

        public ArtikelEntitaet? OhneNummer(string normalisiert) => _ohneNummer.GetValueOrDefault(normalisiert);

        public void Hinzufuegen(ArtikelEntitaet artikel)
        {
            if (artikel.Artikelnummer is null)
            {
                _ohneNummer[artikel.NameNormalisiert] = artikel;
                return;
            }

            _nachNummer[artikel.Artikelnummer] = artikel;
            if (!_mitNummerNachName.TryGetValue(artikel.NameNormalisiert, out var gleichnamig))
            {
                _mitNummerNachName[artikel.NameNormalisiert] = gleichnamig = [];
            }

            gleichnamig.Add(artikel);
        }
    }
}
