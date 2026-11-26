using System.Text.RegularExpressions;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.TagsKategorien;

/// <summary>Liste mit Jahreskennzahlen und Pflege der Kategorien.</summary>
public sealed partial class KategorieVerwaltung(BiberDbContext db, TimeProvider zeit)
{
    public const int EuerZeileMaxLaenge = 100;

    /// <summary>Farben des Designs, reihum vergeben, wenn beim Anlegen keine angegeben ist.</summary>
    public static readonly IReadOnlyList<string> StandardFarben =
        ["#5B8DEF", "#1F8A70", "#3BA55C", "#E3893B", "#D64541", "#8B5CF6", "#9AA4B5"];

    public async Task<IReadOnlyList<KategorieDto>> ListeAsync(int? jahr, CancellationToken ct)
    {
        var j = jahr ?? zeit.GetUtcNow().Year;
        var von = new DateOnly(j, 1, 1);
        var bis = new DateOnly(j, 12, 31);

        var kennzahlen = await db.Belege.AsNoTracking()
            .Where(b => b.KategorieId != null && b.DuplikatVonId == null
                && b.Rechnungsdatum >= von && b.Rechnungsdatum <= bis)
            .GroupBy(b => b.KategorieId!.Value)
            .Select(g => new { Id = g.Key, Anzahl = g.Count(), Summe = g.Sum(b => b.Brutto ?? 0m) })
            .ToDictionaryAsync(k => k.Id, ct);

        var kategorien = await db.Kategorien.AsNoTracking().ToListAsync(ct);
        return AlsBaum(kategorien)
            .Select(k => kennzahlen.TryGetValue(k.Id, out var z) ? ZuDto(k, z.Anzahl, z.Summe) : ZuDto(k, 0, 0m))
            .ToList();
    }

    public async Task<Ergebnis<KategorieDto>> AnlegenAsync(KategorieAendernDto dto, CancellationToken ct)
    {
        var kategorie = new Kategorie { Id = Guid.NewGuid() };
        if (string.IsNullOrWhiteSpace(dto.Farbe))
        {
            dto = dto with { Farbe = StandardFarben[await db.Kategorien.CountAsync(ct) % StandardFarben.Count] };
        }

        if (await UebernehmeAsync(kategorie, dto, ct) is { } fehler)
        {
            return fehler;
        }

        db.Kategorien.Add(kategorie);
        await db.SaveChangesAsync(ct);
        return ZuDto(kategorie, 0, 0m);
    }

    public async Task<Ergebnis<KategorieDto>> AendernAsync(Guid id, KategorieAendernDto dto, CancellationToken ct)
    {
        var kategorie = await db.Kategorien.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kategorie is null)
        {
            return Fehler.NichtGefunden();
        }

        if (await UebernehmeAsync(kategorie, dto, ct) is { } fehler)
        {
            return fehler;
        }

        await db.SaveChangesAsync(ct);
        return (await ListeAsync(null, ct)).First(k => k.Id == id);
    }

    public async Task<Ergebnis<KategorieGeloeschtDto>> LoeschenAsync(Guid id, CancellationToken ct)
    {
        var kategorie = await db.Kategorien.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kategorie is null)
        {
            return Fehler.NichtGefunden();
        }

        // Über verfolgte Entitäten, damit der Audit-Interceptor die Änderungen protokolliert.
        var belege = await db.Belege.Where(b => b.KategorieId == id).ToListAsync(ct);
        belege.ForEach(b => b.KategorieId = null);
        var korrespondenten = await db.Korrespondenten.Where(k => k.DefaultKategorieId == id).ToListAsync(ct);
        korrespondenten.ForEach(k => k.DefaultKategorieId = null);
        var unterkategorien = await db.Kategorien.Where(k => k.ElternId == id).ToListAsync(ct);
        unterkategorien.ForEach(k => k.ElternId = null);
        db.Kategorien.Remove(kategorie);
        await db.SaveChangesAsync(ct);
        return new KategorieGeloeschtDto(belege.Count, korrespondenten.Count);
    }

    private async Task<Fehler?> UebernehmeAsync(Kategorie kategorie, KategorieAendernDto dto, CancellationToken ct)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            return Fehler.Ungueltig("name", "Der Name ist Pflicht.");
        }

        if (dto.Farbe is null || !HexFarbe().IsMatch(dto.Farbe))
        {
            return Fehler.Ungueltig("farbe", "Die Farbe muss als #RRGGBB angegeben sein.");
        }

        var euerZeile = string.IsNullOrWhiteSpace(dto.EuerZeile) ? null : dto.EuerZeile.Trim();
        if (euerZeile?.Length > EuerZeileMaxLaenge)
        {
            return Fehler.Ungueltig("euerZeile", $"Die EÜR-Zeile darf höchstens {EuerZeileMaxLaenge} Zeichen haben.");
        }

        if (MatchingPruefung.Pruefe(dto.Matching) is { } fehler)
        {
            return fehler;
        }

        if (await ElternFehlerAsync(kategorie, dto.ElternId, ct) is { } elternFehler)
        {
            return elternFehler;
        }

        var kleinName = name.ToLower();
        if (await db.Kategorien.AnyAsync(k => k.Id != kategorie.Id && k.Name.ToLower() == kleinName, ct))
        {
            return Fehler.Konflikt($"Eine Kategorie „{name}“ gibt es schon.");
        }

        kategorie.Name = name;
        kategorie.Farbe = dto.Farbe.ToUpperInvariant();
        kategorie.EuerZeile = euerZeile;
        kategorie.ElternId = dto.ElternId;
        var matching = MatchingPruefung.ZuEntitaet(dto.Matching);
        kategorie.Matching.Algorithmus = matching.Algorithmus;
        kategorie.Matching.Muster = matching.Muster;
        kategorie.Matching.GrossKlein = matching.GrossKlein;
        return null;
    }

    private static KategorieDto ZuDto(Kategorie k, int anzahl, decimal summe) =>
        new(k.Id, k.Name, k.Farbe, k.EuerZeile, MatchingPruefung.ZuDto(k.Matching), anzahl, summe, k.ElternId);

    /// <summary>Nur eine Ebene: die Oberkategorie muss existieren, darf selbst keine haben, und wer Unterkategorien hat, wird keine.</summary>
    private async Task<Fehler?> ElternFehlerAsync(Kategorie kategorie, Guid? elternId, CancellationToken ct)
    {
        if (elternId is not { } id)
        {
            return null;
        }

        if (id == kategorie.Id)
        {
            return Fehler.Ungueltig("elternId", "Eine Kategorie kann nicht ihre eigene Oberkategorie sein.");
        }

        var eltern = await db.Kategorien.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct);
        if (eltern is null)
        {
            return Fehler.Ungueltig("elternId", "Die Oberkategorie gibt es nicht.");
        }

        if (eltern.ElternId is not null)
        {
            return Fehler.Ungueltig("elternId", $"„{eltern.Name}“ ist selbst eine Unterkategorie.");
        }

        if (await db.Kategorien.AnyAsync(k => k.ElternId == kategorie.Id, ct))
        {
            return Fehler.Ungueltig("elternId", "Eine Kategorie mit Unterkategorien kann keine Unterkategorie werden.");
        }

        return null;
    }

    /// <summary>Hauptkategorien alphabetisch, jeweils gefolgt von ihren Unterkategorien.</summary>
    private static IEnumerable<Kategorie> AlsBaum(IReadOnlyList<Kategorie> kategorien)
    {
        var vergleich = StringComparer.CurrentCultureIgnoreCase;
        var ids = kategorien.Select(k => k.Id).ToHashSet();
        var kinder = kategorien.Where(k => k.ElternId is { } e && ids.Contains(e)).ToLookup(k => k.ElternId!.Value);
        foreach (var haupt in kategorien.Where(k => k.ElternId is not { } e || !ids.Contains(e)).OrderBy(k => k.Name, vergleich))
        {
            yield return haupt;
            foreach (var kind in kinder[haupt.Id].OrderBy(k => k.Name, vergleich))
            {
                yield return kind;
            }
        }
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexFarbe();
}
