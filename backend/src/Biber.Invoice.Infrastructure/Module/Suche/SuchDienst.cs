using System.Text.RegularExpressions;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Artikel;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Skonto;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.Infrastructure.Persistenz.Konfiguration;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Biber.Invoice.Infrastructure.Module.Suche;

/// <summary>
/// Die vier Abfragen der globalen Suche laufen nacheinander auf demselben <see cref="BiberDbContext"/>.
/// Nicht benötigte Kennzahlen der Listen-DTOs bleiben leer bzw. 0.
/// </summary>
public sealed partial class SuchDienst(BiberDbContext db, TimeProvider zeit) : ISuchDienst
{
    private const int MaxAuszug = 80;
    private const int Max = ISuchDienst.MaxTreffer;

    public async Task<SuchergebnisDto> SuchenAsync(string? q, CancellationToken ct)
    {
        var begriff = Bereinigen(q);
        if (begriff.Length < ISuchDienst.MinLaenge)
        {
            return new SuchergebnisDto([], [], [], []);
        }

        var muster = $"%{LikeMaskieren(begriff)}%";
        var belege = await BelegeAsync(begriff, muster, ct);
        var korrespondenten = await KorrespondentenAsync(begriff, muster, ct);
        var artikel = await ArtikelAsync(begriff, muster, ct);
        var stammartikel = await StammartikelAsync(muster, ct);
        return new SuchergebnisDto(belege, korrespondenten, artikel, stammartikel);
    }

    private async Task<BelegListeneintragDto[]> BelegeAsync(string begriff, string muster, CancellationToken ct)
    {
        var woerter = WortRegex().Matches(begriff).Select(m => m.Value).ToArray();
        var praefixText = string.Join(" & ", woerter.Select((w, i) => i == woerter.Length - 1 ? $"{w}:*" : w));
        var hatWoerter = woerter.Length > 0;
        var heute = DateOnly.FromDateTime(zeit.GetUtcNow().UtcDateTime);

        var zeilen = await db.Belege
            .AsNoTracking()
            .Where(b =>
                EF.Property<NpgsqlTsVector>(b, BelegKonfiguration.Suchvektor).Matches(EF.Functions.WebSearchToTsQuery("german", begriff))
                || (hatWoerter && EF.Property<NpgsqlTsVector>(b, BelegKonfiguration.Suchvektor).Matches(EF.Functions.ToTsQuery("german", praefixText)))
                || EF.Functions.ILike(b.Rechnungsnummer!, muster)
                || b.Positionen.Any(p => EF.Functions.ILike(p.Text, muster)))
            .OrderByDescending(b => hatWoerter
                ? EF.Property<NpgsqlTsVector>(b, BelegKonfiguration.Suchvektor).Rank(EF.Functions.ToTsQuery("german", praefixText))
                : 0f)
            .ThenByDescending(b => b.Rechnungsdatum)
            .ThenBy(b => b.Id)
            .Take(Max)
            .Select(b => new
            {
                b.Id,
                b.Rechnungsnummer,
                b.Titel,
                Korrespondent = b.Korrespondent == null
                    ? null
                    : new { b.Korrespondent.Id, b.Korrespondent.Name, b.Korrespondent.LogoQuelle },
                Kategorie = b.Kategorie == null ? null : new VerweisDto(b.Kategorie.Id, b.Kategorie.Name),
                b.Rechnungsdatum,
                b.Faelligkeitsdatum,
                b.Netto,
                b.Brutto,
                b.Waehrung,
                b.Seitenanzahl,
                b.Quelle,
                b.DiKonfidenz,
                b.Leistungsdatum,
                b.SkontoProzent,
                b.SkontoBis,
                b.SkontoBetrag,
                b.DuplikatVonId,
                Tags = b.Tags
                    .Select(t => new TagKurzDto(t.Tag!.Id, t.Tag.Name, t.Tag.Farbe, t.Tag.IstInbox, t.Tag.IstZahlungsTag, t.Tag.IstSystem))
                    .ToList(),
                Dateien = b.Dateien.Select(d => new { d.Rolle, d.MimeType }).ToList(),
                TrefferPosition = b.Positionen
                    .Where(p => EF.Functions.ILike(p.Text, muster))
                    .OrderBy(p => p.Nr)
                    .Select(p => p.Text)
                    .FirstOrDefault(),
                b.Volltext,
            })
            .ToListAsync(ct);

        return zeilen.Select(z =>
        {
            var tags = z.Tags.ToArray();
            var bezahlt = tags.Any(t => t.IstZahlungsTag);
            return new BelegListeneintragDto(
                z.Id,
                z.Rechnungsnummer,
                z.Korrespondent is null ? null : new VerweisDto(z.Korrespondent.Id, z.Korrespondent.Name),
                z.Korrespondent is null
                    ? new LogoDto(null, "?", LogoQuelle.Keine)
                    : Logo(z.Korrespondent.Id, z.Korrespondent.Name, z.Korrespondent.LogoQuelle),
                z.Titel ?? z.Rechnungsnummer ?? z.Korrespondent?.Name ?? "",
                z.Kategorie,
                z.Rechnungsdatum,
                z.Faelligkeitsdatum,
                !bezahlt && z.Faelligkeitsdatum < heute,
                z.Netto,
                z.Brutto,
                z.Waehrung,
                z.Seitenanzahl,
                z.Quelle,
                Format(z.Dateien.Select(d => (d.Rolle, d.MimeType))),
                tags,
                Auszug(z.TrefferPosition, z.Volltext, woerter, z.Titel),
                z.Dateien.Any(d => d.Rolle == DateiRolle.Vorschau)
                    ? ApiRouten.Belege.Vorschau.Replace("{id}", z.Id.ToString())
                    : null,
                z.DuplikatVonId is not null,
                z.DiKonfidenz,
                z.Leistungsdatum,
                SkontoAnzeige.Erzeugen(z.SkontoProzent, z.SkontoBis, z.SkontoBetrag, heute));
        }).ToArray();
    }

    private async Task<KorrespondentListeneintragDto[]> KorrespondentenAsync(string begriff, string muster, CancellationToken ct)
    {
        var zeilen = await db.Korrespondenten
            .FromSql($"""
                SELECT * FROM korrespondent
                WHERE name ILIKE {muster}
                   OR ust_id_nr ILIKE {muster}
                   OR name % {begriff}
                   OR EXISTS (SELECT 1 FROM unnest(aliase) AS alias WHERE alias ILIKE {muster})
                """)
            .AsNoTracking()
            .OrderByDescending(k => EF.Functions.TrigramsSimilarity(k.Name, begriff))
            .ThenBy(k => k.Name)
            .ThenBy(k => k.Id)
            .Take(Max)
            .Select(k => new
            {
                k.Id,
                k.Name,
                k.LogoQuelle,
                k.UstIdNr,
                k.Steuernummer,
                k.Geprueft,
                DefaultKategorie = k.DefaultKategorie == null ? null : new VerweisDto(k.DefaultKategorie.Id, k.DefaultKategorie.Name),
                k.Matching.Algorithmus,
                k.Matching.Muster,
                k.Matching.GrossKlein,
            })
            .ToListAsync(ct);

        return zeilen.Select(k => new KorrespondentListeneintragDto(
            k.Id,
            k.Name,
            Logo(k.Id, k.Name, k.LogoQuelle),
            null,
            k.UstIdNr,
            k.Steuernummer,
            k.DefaultKategorie,
            new MatchingDto(k.Algorithmus, k.Muster, k.GrossKlein),
            0,
            0m,
            null,
            !k.Geprueft)).ToArray();
    }

    private async Task<ArtikelListeneintragDto[]> ArtikelAsync(string begriff, string muster, CancellationToken ct)
    {
        var normalisiert = begriff.ToLowerInvariant();
        var zeilen = await db.Artikel
            .AsNoTracking()
            .Where(a => EF.Functions.ILike(a.NameNormalisiert, muster)
                || EF.Functions.TrigramsAreSimilar(a.NameNormalisiert, normalisiert)
                || (a.Artikelnummer != null && EF.Functions.ILike(a.Artikelnummer, muster)))
            .OrderByDescending(a => EF.Functions.TrigramsSimilarity(a.NameNormalisiert, normalisiert))
            .ThenBy(a => a.Name)
            .ThenBy(a => a.Id)
            .Take(Max)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Artikelnummer,
                Korrespondent = new VerweisDto(a.Korrespondent!.Id, a.Korrespondent.Name),
                Stammartikel = a.Stammartikel == null ? null : new VerweisDto(a.Stammartikel.Id, a.Stammartikel.Name),
                a.Einheit,
            })
            .ToListAsync(ct);

        return zeilen.Select(a => new ArtikelListeneintragDto(
            a.Id, a.Name, a.Artikelnummer, a.Korrespondent, a.Stammartikel, 0, null, a.Einheit, null, null, null, null)).ToArray();
    }

    private async Task<StammartikelListeneintragDto[]> StammartikelAsync(string muster, CancellationToken ct)
    {
        var zeilen = await db.Stammartikel
            .AsNoTracking()
            .Where(s => EF.Functions.ILike(s.Name, muster))
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Take(Max)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Basiseinheit,
                s.GrundpreisMenge,
                AnzahlArtikel = db.Artikel.Count(a => a.StammartikelId == s.Id),
                Korrespondenten = db.Artikel
                    .Where(a => a.StammartikelId == s.Id)
                    .Select(a => a.Korrespondent!.Name)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList(),
            })
            .ToListAsync(ct);

        return zeilen.Select(s => new StammartikelListeneintragDto(
            s.Id, s.Name, s.Basiseinheit, s.GrundpreisMenge, s.AnzahlArtikel, [.. s.Korrespondenten], [], null, null, null)).ToArray();
    }

    /// <summary>Trimmt und entfernt Steuerzeichen (Postgres verträgt kein NUL in Text).</summary>
    private static string Bereinigen(string? q) =>
        q is null ? "" : new string(q.Where(c => !char.IsControl(c)).ToArray()).Trim();

    private static string LikeMaskieren(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    private static LogoDto Logo(Guid id, string name, LogoQuelle quelle)
    {
        var initialen = string.Concat(
            name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => char.IsLetterOrDigit(w[0]))
                .Take(2)
                .Select(w => char.ToUpperInvariant(w[0])));
        return new LogoDto(
            quelle == LogoQuelle.Keine ? null : ApiRouten.Korrespondenten.Logo.Replace("{id}", id.ToString()),
            initialen.Length == 0 ? "?" : initialen,
            quelle);
    }

    private static string Format(IEnumerable<(DateiRolle Rolle, string MimeType)> dateien)
    {
        var liste = dateien.ToList();
        if (liste.Any(d => d.Rolle == DateiRolle.EingebettetesXml))
        {
            return "ZUGFeRD";
        }

        var original = liste.FirstOrDefault(d => d.Rolle == DateiRolle.Original).MimeType ?? "";
        if (original.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            return "XRechnung";
        }

        return original.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "Scan" : "PDF";
    }

    /// <summary>Position mit dem Treffer, sonst die Textstelle im Volltext, sonst der Titel; höchstens 80 Zeichen.</summary>
    private static string Auszug(string? position, string? volltext, string[] woerter, string? titel)
    {
        if (!string.IsNullOrWhiteSpace(position))
        {
            return Kuerzen(position.Trim());
        }

        if (!string.IsNullOrEmpty(volltext))
        {
            foreach (var wort in woerter.OrderByDescending(w => w.Length))
            {
                var index = volltext.IndexOf(wort, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    continue;
                }

                var start = Math.Max(0, index - (MaxAuszug / 4));
                var ende = Math.Min(volltext.Length, start + MaxAuszug);
                return Kuerzen(Leerraum().Replace(volltext[start..ende], " ").Trim());
            }
        }

        return Kuerzen(titel?.Trim() ?? "");
    }

    private static string Kuerzen(string text) => text.Length <= MaxAuszug ? text : text[..MaxAuszug];

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WortRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Leerraum();
}
