using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.TagsKategorien;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Zuordnung;

/// <summary>
/// Kategorie: Default-Kategorie des Korrespondenten vor der ersten passenden Kategorie-Regel (nach Name);
/// manuell gesetzte Kategorien bleiben. Tags: jeder passende Nicht-System-Tag wird ergänzt, nie entfernt.
/// </summary>
public sealed class ZuordnungsDienst(BiberDbContext db, TimeProvider zeit) : IZuordnungsDienst
{
    public const string ZielTag = "Tag";
    public const string ZielKategorie = "Kategorie";
    public const string ZielKorrespondent = "Korrespondent";

    private const int AuszugLaenge = 120;

    public async Task ZuordnenAsync(Beleg beleg, CancellationToken ct)
    {
        if (!beleg.KategorieManuell)
        {
            var kategorieId = await DefaultKategorieAsync(beleg, ct);
            if (kategorieId is null)
            {
                var kategorien = await db.Kategorien.OrderBy(k => k.Name).ThenBy(k => k.Id).ToListAsync(ct);
                kategorieId = kategorien.FirstOrDefault(k => Aktiv(k.Matching) && k.Matching.Trifft(beleg.Volltext))?.Id;
            }

            if (kategorieId is not null)
            {
                beleg.KategorieId = kategorieId;
            }
        }

        var tags = await db.Tags.Where(t => !t.IstSystem).ToListAsync(ct);
        var gesetzt = beleg.Tags.Select(t => t.TagId).ToHashSet();
        gesetzt.UnionWith(await db.BelegTags.Where(t => t.BelegId == beleg.Id).Select(t => t.TagId).ToListAsync(ct));
        foreach (var tag in tags.Where(t => Aktiv(t.Matching) && !gesetzt.Contains(t.Id) && t.Matching.Trifft(beleg.Volltext)))
        {
            beleg.Tags.Add(new BelegTag { BelegId = beleg.Id, TagId = tag.Id, GesetztAm = zeit.GetUtcNow(), GesetztVonId = null });
        }
    }

    public async Task<MatchingTestErgebnisDto> TestenAsync(MatchingTestDto anfrage, CancellationToken ct)
    {
        var ziel = Normalisieren(anfrage.Ziel);
        var matching = MatchingPruefung.ZuEntitaet(anfrage.Matching);
        if (!Aktiv(matching))
        {
            return new MatchingTestErgebnisDto(0, 0, 0, null, null, null, null);
        }

        var belege = await Bestand()
            .Select(b => new
            {
                b.Id,
                b.Volltext,
                b.Rechnungsnummer,
                b.Rechnungsdatum,
                b.ErstelltAm,
                b.KategorieId,
                b.KorrespondentId,
                TagIds = b.Tags.Select(t => t.TagId).ToList(),
            })
            .ToListAsync(ct);

        var treffer = belege.Where(b => matching.Trifft(b.Volltext)).ToList();
        var ohneZuweisung = anfrage.ZielId is not { } zielId
            ? treffer.Count
            : treffer.Count(b => ziel switch
            {
                ZielTag => !b.TagIds.Contains(zielId),
                ZielKategorie => b.KategorieId != zielId,
                _ => b.KorrespondentId != zielId,
            });

        var juengster = treffer.OrderByDescending(b => b.Rechnungsdatum).ThenByDescending(b => b.ErstelltAm).FirstOrDefault();
        if (juengster is null)
        {
            return new MatchingTestErgebnisDto(0, belege.Count, 0, null, null, null, null);
        }

        var text = juengster.Volltext!.ReplaceLineEndings(" ");
        var stelle = TrefferStelle.Finden(matching, text);
        var mitte = stelle?.Start ?? 0;
        var start = Math.Max(0, Math.Min(mitte - ((AuszugLaenge - (stelle?.Laenge ?? 0)) / 2), text.Length - AuszugLaenge));
        var auszug = text.Substring(start, Math.Min(AuszugLaenge, text.Length - start));
        int? trefferStart = stelle is null ? null : stelle.Value.Start - start;
        int? trefferLaenge = stelle is null ? null : Math.Min(stelle.Value.Laenge, auszug.Length - trefferStart!.Value);

        return new MatchingTestErgebnisDto(
            treffer.Count, belege.Count, ohneZuweisung, auszug, trefferStart, trefferLaenge, juengster.Rechnungsnummer);
    }

    public async Task<int> AnwendenAsync(string ziel, Guid zielId, CancellationToken ct)
    {
        switch (Normalisieren(ziel))
        {
            case ZielTag:
                {
                    var tag = await db.Tags.SingleOrDefaultAsync(t => t.Id == zielId, ct) ?? throw new KeyNotFoundException("Tag nicht gefunden.");
                    if (tag.IstSystem || !Aktiv(tag.Matching))
                    {
                        return 0;
                    }

                    var belege = await Bestand().Where(b => !b.Tags.Any(t => t.TagId == zielId)).ToListAsync(ct);
                    var passend = belege.Where(b => tag.Matching.Trifft(b.Volltext)).ToList();
                    db.BelegTags.AddRange(passend.Select(b => new BelegTag
                    {
                        BelegId = b.Id,
                        TagId = zielId,
                        GesetztAm = zeit.GetUtcNow(),
                        GesetztVonId = null,
                    }));
                    await db.SaveChangesAsync(ct);
                    return passend.Count;
                }

            case ZielKategorie:
                {
                    var kategorie = await db.Kategorien.SingleOrDefaultAsync(k => k.Id == zielId, ct)
                        ?? throw new KeyNotFoundException("Kategorie nicht gefunden.");
                    if (!Aktiv(kategorie.Matching))
                    {
                        return 0;
                    }

                    var belege = await Bestand().Where(b => b.KategorieId == null && !b.KategorieManuell).ToListAsync(ct);
                    var passend = belege.Where(b => kategorie.Matching.Trifft(b.Volltext)).ToList();
                    passend.ForEach(b => b.KategorieId = zielId);
                    await db.SaveChangesAsync(ct);
                    return passend.Count;
                }

            default:
                throw new ArgumentException("Anwenden ist nur für Tags und Kategorien möglich.", nameof(ziel));
        }
    }

    private IQueryable<Beleg> Bestand() =>
        db.Belege.Where(b => b.Status == BelegStatus.Fertig && b.DuplikatVonId == null && b.Volltext != null);

    private async Task<Guid?> DefaultKategorieAsync(Beleg beleg, CancellationToken ct)
    {
        if (beleg.Korrespondent is { } korrespondent)
        {
            return korrespondent.DefaultKategorieId;
        }

        return beleg.KorrespondentId is { } id
            ? await db.Korrespondenten.Where(k => k.Id == id).Select(k => k.DefaultKategorieId).SingleOrDefaultAsync(ct)
            : null;
    }

    private static bool Aktiv(Matching m) => m.Algorithmus is not (MatchingAlgorithmus.Keiner or MatchingAlgorithmus.Auto);

    private static string Normalisieren(string? ziel) => ziel?.Trim().ToLowerInvariant() switch
    {
        "tag" => ZielTag,
        "kategorie" => ZielKategorie,
        "korrespondent" => ZielKorrespondent,
        _ => throw new ArgumentException("Unbekanntes Ziel; erlaubt sind „Tag“, „Kategorie“ und „Korrespondent“.", nameof(ziel)),
    };
}
