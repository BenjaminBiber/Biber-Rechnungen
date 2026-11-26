using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.TagsKategorien;

/// <summary>
/// Liste und Pflege der Tags. Höchstens ein Tag ist Inbox-Tag; Inbox- und Zahlungs-Tag schließen sich aus;
/// System-Tags sind unveränderbar.
/// </summary>
public sealed class TagVerwaltung(BiberDbContext db)
{
    public const int NameMaxLaenge = 50;

    public static readonly IReadOnlySet<string> Farben =
        new HashSet<string>(["blau", "orange", "gruen", "rot", "grau", "petrol", "lila"], StringComparer.Ordinal);

    public async Task<IReadOnlyList<TagDto>> ListeAsync(CancellationToken ct)
    {
        var tags = await db.Tags.AsNoTracking()
            // Über db.Belege, damit gelöschte (falsch zugeordnete) Belege nicht mitzählen.
            .Select(t => new
            {
                Tag = t,
                Anzahl = db.BelegTags.Count(bt => bt.TagId == t.Id && db.Belege.Any(b => b.Id == bt.BelegId)),
            })
            .ToListAsync(ct);

        return tags
            .OrderByDescending(t => t.Tag.IstInbox)
            .ThenByDescending(t => t.Tag.IstSystem)
            .ThenBy(t => t.Tag.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(t => ZuDto(t.Tag, t.Anzahl))
            .ToList();
    }

    public async Task<Ergebnis<TagDto>> AnlegenAsync(TagAendernDto dto, CancellationToken ct)
    {
        var tag = new Tag { Id = Guid.NewGuid() };
        if (await UebernehmeAsync(tag, dto, ct) is { } fehler)
        {
            return fehler;
        }

        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return ZuDto(tag, 0);
    }

    public async Task<Ergebnis<TagDto>> AendernAsync(Guid id, TagAendernDto dto, CancellationToken ct)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
        {
            return Fehler.NichtGefunden();
        }

        if (tag.IstSystem)
        {
            return Fehler.Konflikt("System-Tags lassen sich nicht ändern.");
        }

        if (await UebernehmeAsync(tag, dto, ct) is { } fehler)
        {
            return fehler;
        }

        await db.SaveChangesAsync(ct);
        return ZuDto(tag, await db.BelegTags.CountAsync(
            bt => bt.TagId == id && db.Belege.Any(b => b.Id == bt.BelegId), ct));
    }

    public async Task<Fehler?> LoeschenAsync(Guid id, CancellationToken ct)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
        {
            return Fehler.NichtGefunden();
        }

        if (tag.IstSystem)
        {
            return Fehler.Konflikt("System-Tags lassen sich nicht löschen.");
        }

        // Zuordnungen einzeln entfernen, damit der Audit-Interceptor sie protokolliert.
        db.BelegTags.RemoveRange(await db.BelegTags.Where(bt => bt.TagId == id).ToListAsync(ct));
        db.Tags.Remove(tag);
        await db.SaveChangesAsync(ct);
        return null;
    }

    private async Task<Fehler?> UebernehmeAsync(Tag tag, TagAendernDto dto, CancellationToken ct)
    {
        var name = dto.Name?.Trim() ?? "";
        if (name.Length == 0)
        {
            return Fehler.Ungueltig("name", "Der Name ist Pflicht.");
        }

        if (name.Length > NameMaxLaenge)
        {
            return Fehler.Ungueltig("name", $"Der Name darf höchstens {NameMaxLaenge} Zeichen haben.");
        }

        if (dto.Farbe is null || !Farben.Contains(dto.Farbe))
        {
            return Fehler.Ungueltig("farbe", "Unbekannte Farbe.");
        }

        if (dto.IstInbox && dto.IstZahlungsTag)
        {
            return Fehler.Ungueltig("istZahlungsTag", "Ein Tag kann nicht zugleich Inbox- und Zahlungs-Tag sein.");
        }

        if (MatchingPruefung.Pruefe(dto.Matching) is { } fehler)
        {
            return fehler;
        }

        var kleinName = name.ToLower();
        if (await db.Tags.AnyAsync(t => t.Id != tag.Id && t.Name.ToLower() == kleinName, ct))
        {
            return Fehler.Konflikt($"Ein Tag „{name}“ gibt es schon.");
        }

        if (dto.IstInbox)
        {
            var bisherige = await db.Tags.Where(t => t.Id != tag.Id && t.IstInbox).ToListAsync(ct);
            bisherige.ForEach(t => t.IstInbox = false);
        }

        tag.Name = name;
        tag.Farbe = dto.Farbe;
        tag.IstInbox = dto.IstInbox;
        tag.IstZahlungsTag = dto.IstZahlungsTag;
        var matching = MatchingPruefung.ZuEntitaet(dto.Matching);
        tag.Matching.Algorithmus = matching.Algorithmus;
        tag.Matching.Muster = matching.Muster;
        tag.Matching.GrossKlein = matching.GrossKlein;
        return null;
    }

    private static TagDto ZuDto(Tag t, int anzahl) =>
        new(t.Id, t.Name, t.Farbe, t.IstInbox, t.IstZahlungsTag, t.IstSystem, MatchingPruefung.ZuDto(t.Matching), anzahl);
}
