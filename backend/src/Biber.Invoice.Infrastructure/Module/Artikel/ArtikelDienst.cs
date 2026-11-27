using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Artikel;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using ArtikelEntitaet = Biber.Invoice.Domain.Entitaeten.Artikel;
using Merkmal = Biber.Invoice.Domain.Entitaeten.Merkmal;
using StammartikelEntitaet = Biber.Invoice.Domain.Entitaeten.Stammartikel;

// Namespace bewusst nicht „Module.Artikel“: er würde in allen Nachbarmodulen den Entitätstyp Artikel verdecken.
namespace Biber.Invoice.Infrastructure.Module.Artikelpreise;

/// <summary>
/// Preisstatistik über Käufe. Ein Kauf ist eine Position mit Artikel in einem fertigen Beleg, der keine Dublette ist;
/// sein Datum ist das Rechnungsdatum. Die Kennzahlen werden nach dem Laden der Käufe im Speicher berechnet.
/// </summary>
public sealed class ArtikelDienst(BiberDbContext db, TimeProvider zeit) : IArtikelDienst
{
    private const int VerlaufLaenge = 8;

    public async Task<SeiteDto<ArtikelListeneintragDto>> SuchenAsync(ArtikelFilterDto filter, CancellationToken ct)
    {
        var abfrage = db.Artikel.AsNoTracking();
        var suche = filter.Suche?.Trim();
        if (!string.IsNullOrEmpty(suche))
        {
            var normalisiert = suche.ToLowerInvariant();
            var muster = $"%{LikeMaskieren(normalisiert)}%";
            abfrage = abfrage.Where(a => EF.Functions.ILike(a.NameNormalisiert, muster)
                || EF.Functions.TrigramsAreSimilar(a.NameNormalisiert, normalisiert)
                || (a.Artikelnummer != null && EF.Functions.ILike(a.Artikelnummer, muster)));
        }

        if (filter.KorrespondentId is { } korrespondentId)
        {
            abfrage = abfrage.Where(a => a.KorrespondentId == korrespondentId);
        }

        if (filter.OhneStammartikel)
        {
            abfrage = abfrage.Where(a => a.StammartikelId == null);
        }

        var artikel = await ArtikelInfosAsync(abfrage, ct);
        var kaeufe = await KaeufeAsync(artikel, ct);

        var eintraege = artikel.Values
            .Select(a => Listeneintrag(a, kaeufe.TryGetValue(a.Id, out var k) ? k : []))
            .Where(e => filter.Preisaenderung switch
            {
                PreisaenderungFilter.Gestiegen => e.AenderungSeitErstkauf > 0,
                PreisaenderungFilter.Gefallen => e.AenderungSeitErstkauf < 0,
                _ => true,
            })
            .OrderByDescending(e => e.ZuletztGekauft.HasValue)
            .ThenByDescending(e => e.ZuletztGekauft)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(e => e.Id)
            .ToList();

        var seite = eintraege
            .Skip((filter.Seite - 1) * filter.Seitengroesse)
            .Take(filter.Seitengroesse)
            .ToList();
        return new SeiteDto<ArtikelListeneintragDto>(seite, eintraege.Count, filter.Seite, filter.Seitengroesse);
    }

    public async Task<PreisDetailDto?> ArtikelDetailAsync(Guid id, int jahr, CancellationToken ct)
    {
        var artikel = await ArtikelInfosAsync(db.Artikel.AsNoTracking().Where(a => a.Id == id), ct);
        if (!artikel.TryGetValue(id, out var info))
        {
            return null;
        }

        var kaeufe = (await KaeufeAsync(artikel, ct)).GetValueOrDefault(id, []);
        var imJahr = kaeufe.Where(k => k.Datum.Year == jahr).ToList();
        var guenstigster = imJahr.Where(k => k.Grundpreis is not null).MinBy(k => k.Grundpreis);
        var erster = imJahr.FirstOrDefault(k => k.Einzelpreis is not null);
        var letzter = imJahr.LastOrDefault(k => k.Einzelpreis is not null);

        var kennzahlen = new PreisKennzahlenDto(
            guenstigster?.Grundpreis,
            guenstigster?.Artikel.KorrespondentName,
            StammartikelRechner.Aenderung(erster?.Einzelpreis, letzter?.Einzelpreis),
            erster?.Einzelpreis,
            letzter?.Einzelpreis,
            imJahr.Sum(k => k.Menge ?? 0m),
            imJahr.Sum(k => k.Netto),
            imJahr.Count);

        return new PreisDetailDto(
            info.Id,
            info.Name,
            info.Artikelnummer,
            IstStammartikel: false,
            info.Basiseinheit,
            info.GrundpreisMenge,
            kennzahlen,
            [Serie(info, imJahr)],
            KaufListe(imJahr),
            [Zugeordnet(info)]);
    }

    public async Task<IReadOnlyList<StammartikelListeneintragDto>> StammartikelListeAsync(CancellationToken ct)
    {
        var stammartikel = await db.Stammartikel.AsNoTracking().OrderBy(s => s.Name).ThenBy(s => s.Id).ToListAsync(ct);
        return await StammartikelEintraegeAsync(stammartikel, ct);
    }

    public async Task<PreisDetailDto?> StammartikelDetailAsync(Guid id, int jahr, CancellationToken ct)
    {
        var stamm = await db.Stammartikel.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (stamm is null)
        {
            return null;
        }

        var artikel = await ArtikelInfosAsync(db.Artikel.AsNoTracking().Where(a => a.StammartikelId == id), ct);
        var kaeufe = await KaeufeAsync(artikel, ct);
        var imJahr = kaeufe.Values.SelectMany(k => k).Where(k => k.Datum.Year == jahr).OrderBy(k => k).ToList();
        var guenstigster = Guenstigster(imJahr);
        var mitGrundpreis = imJahr.Where(k => k.Grundpreis is not null).ToList();
        var von = mitGrundpreis.FirstOrDefault()?.Grundpreis;
        var bis = mitGrundpreis.LastOrDefault()?.Grundpreis;

        var kennzahlen = new PreisKennzahlenDto(
            guenstigster?.Grundpreis,
            guenstigster?.Artikel.KorrespondentName,
            StammartikelRechner.Aenderung(von, bis),
            von,
            bis,
            imJahr.Sum(k => StammartikelRechner.MengeInBasiseinheiten(k.Menge, k.Artikel.UmrechnungFaktor)),
            imJahr.Sum(k => k.Netto),
            imJahr.Count);

        var sortiert = artikel.Values
            .OrderBy(a => a.KorrespondentName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.Id)
            .ToList();

        return new PreisDetailDto(
            stamm.Id,
            stamm.Name,
            Artikelnummer: null,
            IstStammartikel: true,
            stamm.Basiseinheit,
            stamm.GrundpreisMenge,
            kennzahlen,
            [.. sortiert.Select(a => Serie(a, imJahr.Where(k => k.Artikel.Id == a.Id).ToList()))],
            KaufListe(imJahr),
            [.. sortiert.Select(Zugeordnet)]);
    }

    public async Task<StammartikelListeneintragDto?> StammartikelSpeichernAsync(
        Guid? id,
        StammartikelAendernDto stammartikel,
        CancellationToken ct)
    {
        StammartikelEntitaet? stamm;
        if (id is null)
        {
            stamm = new StammartikelEntitaet { Id = Guid.NewGuid() };
            db.Stammartikel.Add(stamm);
        }
        else
        {
            stamm = await db.Stammartikel.SingleOrDefaultAsync(s => s.Id == id, ct);
            if (stamm is null)
            {
                return null;
            }
        }

        stamm.Name = stammartikel.Name.Trim();
        stamm.Basiseinheit = stammartikel.Basiseinheit.Trim();
        stamm.GrundpreisMenge = stammartikel.GrundpreisMenge;

        var zuordnungen = stammartikel.Artikel ?? [];
        var artikelIds = zuordnungen.Select(z => z.ArtikelId).Distinct().ToList();
        var artikel = await db.Artikel.Where(a => artikelIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var fehlend = artikelIds.Where(a => !artikel.ContainsKey(a)).ToList();
        if (fehlend.Count > 0)
        {
            throw new StammartikelUngueltigException(new Dictionary<string, string[]>
            {
                ["artikel"] = [.. fehlend.Select(a => $"Artikel {a} nicht gefunden.")],
            });
        }

        var fremd = artikel.Values.Where(a => a.StammartikelId is not null && a.StammartikelId != stamm.Id).ToList();
        if (fremd.Count > 0)
        {
            throw new StammartikelKonfliktException(
                $"Bereits einem anderen Stammartikel zugeordnet: {string.Join(", ", fremd.Select(a => a.Name))}.");
        }

        foreach (var zuordnung in zuordnungen)
        {
            var eintrag = artikel[zuordnung.ArtikelId];
            eintrag.StammartikelId = stamm.Id;
            eintrag.UmrechnungFaktor = zuordnung.UmrechnungFaktor;
        }

        await db.SaveChangesAsync(ct);
        return (await StammartikelEintraegeAsync([stamm], ct)).Single();
    }

    public async Task<bool> ArtikelHerausloesenAsync(Guid stammartikelId, Guid artikelId, CancellationToken ct)
    {
        var artikel = await db.Artikel.SingleOrDefaultAsync(a => a.Id == artikelId && a.StammartikelId == stammartikelId, ct);
        if (artikel is null)
        {
            return false;
        }

        artikel.StammartikelId = null;
        artikel.UmrechnungFaktor = null;
        if (!await db.Artikel.AnyAsync(a => a.StammartikelId == stammartikelId && a.Id != artikelId, ct))
        {
            var stamm = await db.Stammartikel.SingleAsync(s => s.Id == stammartikelId, ct);
            db.Stammartikel.Remove(stamm);
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<IReadOnlyList<StammartikelListeneintragDto>> StammartikelEintraegeAsync(
        IReadOnlyList<StammartikelEntitaet> stammartikel,
        CancellationToken ct)
    {
        var ids = stammartikel.Select(s => s.Id).ToList();
        var artikel = await ArtikelInfosAsync(
            db.Artikel.AsNoTracking().Where(a => a.StammartikelId != null && ids.Contains(a.StammartikelId.Value)),
            ct);
        var kaeufe = await KaeufeAsync(artikel, ct);
        var jahr = zeit.GetUtcNow().Year;

        return [.. stammartikel.Select(s =>
        {
            var eigene = artikel.Values.Where(a => a.StammartikelId == s.Id).ToList();
            var alle = eigene.SelectMany(a => kaeufe.GetValueOrDefault(a.Id, [])).OrderBy(k => k).ToList();
            var mitGrundpreis = alle.Where(k => k.Grundpreis is not null).ToList();
            var imJahr = mitGrundpreis.Where(k => k.Datum.Year == jahr).ToList();
            var guenstigster = Guenstigster(alle);

            return new StammartikelListeneintragDto(
                s.Id,
                s.Name,
                s.Basiseinheit,
                s.GrundpreisMenge,
                eigene.Count,
                [.. eigene.Select(a => a.KorrespondentName).Distinct().Order(StringComparer.CurrentCultureIgnoreCase)],
                [.. mitGrundpreis.TakeLast(VerlaufLaenge).Select(k => k.Grundpreis!.Value)],
                guenstigster?.Grundpreis,
                guenstigster?.Artikel.KorrespondentName,
                StammartikelRechner.Aenderung(imJahr.FirstOrDefault()?.Grundpreis, imJahr.LastOrDefault()?.Grundpreis));
        })];
    }

    /// <summary>Jüngster Kauf mit Grundpreis je Artikel, davon der günstigste.</summary>
    private static Kauf? Guenstigster(IEnumerable<Kauf> kaeufe) =>
        kaeufe.Where(k => k.Grundpreis is not null)
            .GroupBy(k => k.Artikel.Id)
            .Select(g => g.Max()!)
            .OrderBy(k => k.Grundpreis)
            .ThenBy(k => k.Artikel.KorrespondentName, StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault();

    private static ArtikelListeneintragDto Listeneintrag(ArtikelInfo a, IReadOnlyList<Kauf> kaeufe)
    {
        var mitPreis = kaeufe.Where(k => k.Einzelpreis is not null).ToList();
        var letzter = kaeufe.LastOrDefault();
        var letzterMitPreis = mitPreis.LastOrDefault();
        return new ArtikelListeneintragDto(
            a.Id,
            a.Name,
            a.Artikelnummer,
            new VerweisDto(a.KorrespondentId, a.KorrespondentName),
            a.StammartikelId is null ? null : new VerweisDto(a.StammartikelId.Value, a.StammartikelName!),
            kaeufe.Count,
            letzterMitPreis?.Einzelpreis,
            letzterMitPreis?.Einheit ?? a.Einheit,
            letzterMitPreis?.Grundpreis,
            letzterMitPreis?.GrundpreisEinheit,
            mitPreis.Count < 2 ? null : StammartikelRechner.Aenderung(mitPreis[0].Einzelpreis, letzterMitPreis!.Einzelpreis),
            letzter?.Datum);
    }

    private static PreisSerieDto Serie(ArtikelInfo a, IReadOnlyList<Kauf> kaeufe) => new(
        new VerweisDto(a.KorrespondentId, a.KorrespondentName),
        a.Einheit,
        a.UmrechnungFaktor,
        [.. kaeufe.Order().Select(k => new PreisPunktDto(k.Datum, k.Einzelpreis, k.Grundpreis))]);

    private static KaufDto[] KaufListe(IEnumerable<Kauf> kaeufe) =>
        [.. kaeufe.OrderDescending().Select(k => new KaufDto(
            k.Datum,
            new VerweisDto(k.Artikel.KorrespondentId, k.Artikel.KorrespondentName),
            k.BelegId,
            k.Rechnungsnummer,
            k.Menge,
            k.Einheit,
            k.Einzelpreis,
            k.Grundpreis,
            [.. k.Merkmale.Select(m => new MerkmalDto(m.Name, m.Wert))]))];

    private static ZugeordneterArtikelDto Zugeordnet(ArtikelInfo a) =>
        new(a.Id, a.Name, a.Artikelnummer, new VerweisDto(a.KorrespondentId, a.KorrespondentName), a.Einheit, a.UmrechnungFaktor);

    private static async Task<Dictionary<Guid, ArtikelInfo>> ArtikelInfosAsync(
        IQueryable<ArtikelEntitaet> abfrage,
        CancellationToken ct) =>
        await abfrage
            .Select(a => new ArtikelInfo(
                a.Id,
                a.Name,
                a.Artikelnummer,
                a.KorrespondentId,
                a.Korrespondent!.Name,
                a.Einheit,
                a.StammartikelId,
                a.Stammartikel == null ? null : a.Stammartikel.Name,
                a.Stammartikel == null ? null : a.Stammartikel.Basiseinheit,
                a.Stammartikel == null ? null : (decimal?)a.Stammartikel.GrundpreisMenge,
                a.UmrechnungFaktor))
            .ToDictionaryAsync(a => a.Id, ct);

    /// <summary>Käufe je Artikel, zeitlich aufsteigend.</summary>
    private async Task<Dictionary<Guid, IReadOnlyList<Kauf>>> KaeufeAsync(
        IReadOnlyDictionary<Guid, ArtikelInfo> artikel,
        CancellationToken ct)
    {
        if (artikel.Count == 0)
        {
            return [];
        }

        var ids = artikel.Keys.ToList();
        var zeilen = await (
                from p in db.Positionen.AsNoTracking()
                join b in db.Belege.AsNoTracking() on p.BelegId equals b.Id
                where p.ArtikelId != null
                    && ids.Contains(p.ArtikelId.Value)
                    && b.Status == BelegStatus.Fertig
                    && b.DuplikatVonId == null
                    && b.Rechnungsdatum != null
                select new
                {
                    ArtikelId = p.ArtikelId!.Value,
                    BelegId = b.Id,
                    b.Rechnungsnummer,
                    Datum = b.Rechnungsdatum!.Value,
                    b.ErstelltAm,
                    p.Nr,
                    p.Menge,
                    p.Einheit,
                    p.Einzelpreis,
                    p.Gesamt,
                    p.Grundpreis,
                    p.GrundpreisEinheit,
                    p.Merkmale,
                })
            .ToListAsync(ct);

        return zeilen
            .Select(z =>
            {
                var a = artikel[z.ArtikelId];
                var umgerechnet = a.GrundpreisMenge is not null && a.UmrechnungFaktor is > 0;
                return new Kauf(
                    a,
                    z.BelegId,
                    z.Rechnungsnummer,
                    z.Datum,
                    z.ErstelltAm,
                    z.Nr,
                    z.Menge,
                    z.Einheit ?? a.Einheit,
                    z.Einzelpreis,
                    z.Gesamt ?? (z.Menge is { } m && z.Einzelpreis is { } e ? Math.Round(m * e, 2, MidpointRounding.AwayFromZero) : 0m),
                    umgerechnet
                        ? StammartikelRechner.Grundpreis(z.Einzelpreis, a.UmrechnungFaktor, a.GrundpreisMenge!.Value)
                        : z.Grundpreis,
                    umgerechnet
                        ? StammartikelRechner.GrundpreisEinheit(a.GrundpreisMenge!.Value, a.Basiseinheit!)
                        : z.GrundpreisEinheit,
                    z.Merkmale);
            })
            .GroupBy(k => k.Artikel.Id)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Kauf>)[.. g.Order()]);
    }

    private static string LikeMaskieren(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private sealed record ArtikelInfo(
        Guid Id,
        string Name,
        string? Artikelnummer,
        Guid KorrespondentId,
        string KorrespondentName,
        string? Einheit,
        Guid? StammartikelId,
        string? StammartikelName,
        string? Basiseinheit,
        decimal? GrundpreisMenge,
        decimal? UmrechnungFaktor);

    /// <summary>Ein Kauf; geordnet nach Rechnungsdatum, Importzeitpunkt, Beleg und Positionsnummer.</summary>
    private sealed record Kauf(
        ArtikelInfo Artikel,
        Guid BelegId,
        string? Rechnungsnummer,
        DateOnly Datum,
        DateTimeOffset ErstelltAm,
        int Nr,
        decimal? Menge,
        string? Einheit,
        decimal? Einzelpreis,
        decimal Netto,
        decimal? Grundpreis,
        string? GrundpreisEinheit,
        IReadOnlyList<Merkmal> Merkmale) : IComparable<Kauf>
    {
        public int CompareTo(Kauf? other)
        {
            if (other is null)
            {
                return 1;
            }

            var vergleich = Datum.CompareTo(other.Datum);
            if (vergleich == 0)
            {
                vergleich = ErstelltAm.CompareTo(other.ErstelltAm);
            }

            if (vergleich == 0)
            {
                vergleich = BelegId.CompareTo(other.BelegId);
            }

            return vergleich == 0 ? Nr.CompareTo(other.Nr) : vergleich;
        }
    }
}
