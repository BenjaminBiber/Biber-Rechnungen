using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Artikel;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Korrespondenten;

/// <summary>
/// Korrespondenten mit Kosten und Kennzahlen, Anlegen, Bearbeiten und Zusammenführen.
/// Summen sind immer brutto, nach Rechnungsdatum, ohne Dubletten und nur in EUR.
/// </summary>
public sealed class KorrespondentDienst(BiberDbContext db, TimeProvider zeit) : IKorrespondentDienst
{
    public const string TypLieferanten = "Lieferanten";
    public const string TypKunden = "Kunden";
    public const string TypAlle = "Alle";

    private const int AnzahlLetzteBelege = 5;
    private const int AnzahlArtikelImDetail = 10;

    public async Task<IReadOnlyList<KorrespondentListeneintragDto>> ListeAsync(string? suche, string typ, int jahr, CancellationToken ct)
    {
        var alle = await db.Korrespondenten.AsNoTracking().Include(k => k.DefaultKategorie).ToListAsync(ct);
        var namen = alle
            .GroupBy(k => KorrespondentNormalisierung.Name(k.Name))
            .ToDictionary(g => g.Key, g => g.ToList());

        var gefiltert = alle.Where(k => typ switch
        {
            TypKunden => k.IstKunde,
            TypAlle => true,
            _ => k.IstLieferant,
        });
        if (!string.IsNullOrWhiteSpace(suche))
        {
            gefiltert = gefiltert.Where(k => Trifft(k, suche));
        }

        var auswahl = gefiltert.ToList();
        var ids = auswahl.Select(k => k.Id).ToList();
        var belege = await BelegZeilen(db.Belege.Where(b => b.KorrespondentId != null && ids.Contains(b.KorrespondentId.Value)))
            .ToListAsync(ct);
        var jeKorrespondent = belege.ToLookup(b => b.KorrespondentId!.Value);

        return auswahl
            .Select(k =>
            {
                var eigene = jeKorrespondent[k.Id].ToList();
                var dublette = namen[KorrespondentNormalisierung.Name(k.Name)].FirstOrDefault(a => a.Id != k.Id);
                return new KorrespondentListeneintragDto(
                    k.Id,
                    k.Name,
                    Logo(k),
                    Hinweis(k, dublette),
                    k.UstIdNr,
                    k.Steuernummer,
                    k.DefaultKategorie is { } kat ? new VerweisDto(kat.Id, kat.Name) : null,
                    MatchingDto(k.Matching),
                    eigene.Count,
                    Summe(eigene.Where(b => b.Rechnungsdatum?.Year == jahr)),
                    eigene.Max(b => b.Rechnungsdatum),
                    !k.Geprueft);
            })
            .OrderByDescending(e => e.KostenJahr)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<KorrespondentDetailDto?> AbrufenAsync(Guid id, int jahr, CancellationToken ct)
    {
        var k = await db.Korrespondenten.AsNoTracking().Include(x => x.DefaultKategorie).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (k is null)
        {
            return null;
        }

        var heute = DateOnly.FromDateTime(zeit.GetUtcNow().UtcDateTime);
        var belege = await BelegZeilen(db.Belege.Where(b => b.KorrespondentId == id)).ToListAsync(ct);

        var imJahr = belege.Where(b => b.Rechnungsdatum?.Year == jahr).ToList();
        var kostenJahr = Summe(imJahr);
        var euroImJahr = imJahr.Count(b => b.Waehrung == "EUR");

        // Verglichen wird derselbe Zeitraum: im laufenden Jahr bis zum heutigen Tag, sonst das ganze Jahr.
        var bis = jahr == heute.Year ? heute : new DateOnly(jahr, 12, 31);
        var vorjahrBis = bis.AddYears(-1);
        var bisherJahr = Summe(imJahr.Where(b => b.Rechnungsdatum <= bis));
        var bisherVorjahr = Summe(belege.Where(b => b.Rechnungsdatum?.Year == jahr - 1 && b.Rechnungsdatum <= vorjahrBis));
        decimal? aenderung = bisherVorjahr == 0 ? null : Math.Round((bisherJahr - bisherVorjahr) / bisherVorjahr, 4);

        var erstes = belege.Min(b => b.Rechnungsdatum);
        var offen = belege.Where(b => !b.Bezahlt).ToList();
        var kennzahlen = new KorrespondentKennzahlenDto(
            kostenJahr,
            aenderung,
            belege.Count,
            erstes is { } e ? new DateOnly(e.Year, e.Month, 1) : null,
            euroImJahr == 0 ? 0 : Math.Round(kostenJahr / euroImJahr, 2),
            offen.Count,
            Summe(offen),
            offen.Min(b => b.Faelligkeitsdatum));

        var verlauf = Enumerable.Range(1, 12)
            .Select(monat => Summe(imJahr.Where(b => b.Rechnungsdatum!.Value.Month == monat)))
            .ToArray();

        var autoTrainiertAm = await db.MatchingModelle
            .Where(m => m.Ziel == "Korrespondent")
            .MaxAsync(m => (DateTimeOffset?)m.TrainiertAm, ct);

        var (artikel, anzahlArtikel) = await ArtikelAsync(k, ct);

        return new KorrespondentDetailDto(
            k.Id,
            k.Name,
            [.. k.Aliase],
            AnschriftText(k.Anschrift),
            k.UstIdNr,
            k.Steuernummer,
            [.. k.Ibans],
            k.Website,
            k.Email,
            k.IstLieferant,
            k.IstKunde,
            Logo(k),
            k.DefaultKategorie is { } kat ? new VerweisDto(kat.Id, kat.Name) : null,
            MatchingDto(k.Matching),
            autoTrainiertAm,
            kennzahlen,
            verlauf,
            await LetzteBelegeAsync(k, heute, ct),
            artikel,
            anzahlArtikel);
    }

    public async Task<KorrespondentDetailDto> AnlegenAsync(KorrespondentAendernDto korrespondent, CancellationToken ct)
    {
        var neu = new Korrespondent { Id = Guid.NewGuid(), ErstelltAm = zeit.GetUtcNow() };
        await UebernehmenAsync(neu, korrespondent, ct);
        db.Korrespondenten.Add(neu);
        await db.SaveChangesAsync(ct);
        return (await AbrufenAsync(neu.Id, AktuellesJahr, ct))!;
    }

    public async Task<KorrespondentDetailDto?> AendernAsync(Guid id, KorrespondentAendernDto korrespondent, CancellationToken ct)
    {
        var vorhanden = await db.Korrespondenten.SingleOrDefaultAsync(k => k.Id == id, ct);
        if (vorhanden is null)
        {
            return null;
        }

        await UebernehmenAsync(vorhanden, korrespondent, ct);
        await db.SaveChangesAsync(ct);
        return await AbrufenAsync(id, AktuellesJahr, ct);
    }

    public async Task<KorrespondentDetailDto?> ZusammenfuehrenAsync(Guid behaltenId, IReadOnlyList<Guid> quellIds, CancellationToken ct)
    {
        if (quellIds.Count == 0)
        {
            throw KorrespondentValidierungsAusnahme.Fuer("quellIds", "Mindestens ein Korrespondent zum Zusammenführen ist nötig.");
        }

        if (quellIds.Contains(behaltenId))
        {
            throw KorrespondentValidierungsAusnahme.Fuer("quellIds", "Der behaltene Korrespondent darf nicht unter den Quellen sein.");
        }

        var ids = quellIds.Distinct().ToList();
        var strategie = db.Database.CreateExecutionStrategy();
        return await strategie.ExecuteAsync(async () =>
        {
            await using var transaktion = await db.Database.BeginTransactionAsync(ct);
            var behalten = await db.Korrespondenten.SingleOrDefaultAsync(k => k.Id == behaltenId, ct);
            if (behalten is null)
            {
                throw KorrespondentValidierungsAusnahme.Fuer("behaltenId", "Unbekannter Korrespondent.");
            }

            var quellen = await db.Korrespondenten.Where(k => ids.Contains(k.Id)).ToListAsync(ct);
            if (quellen.Count != ids.Count)
            {
                throw KorrespondentValidierungsAusnahme.Fuer("quellIds", "Mindestens ein Korrespondent ist unbekannt.");
            }

            var belege = await db.Belege.Where(b => b.KorrespondentId != null && ids.Contains(b.KorrespondentId.Value)).ToListAsync(ct);
            foreach (var beleg in belege)
            {
                beleg.KorrespondentId = behaltenId;
            }

            var zuLoeschen = await ArtikelUmhaengenAsync(behalten, ids, ct);

            var aliase = new List<string>(behalten.Aliase);
            var ibans = new List<string>(behalten.Ibans);
            foreach (var quelle in quellen)
            {
                aliase.Add(quelle.Name);
                aliase.AddRange(quelle.Aliase);
                ibans.AddRange(quelle.Ibans);
                FeldFuellen(behalten, quelle);
            }

            behalten.Aliase = OhneDubletten(aliase.Where(a => !string.Equals(a, behalten.Name, StringComparison.OrdinalIgnoreCase)));
            behalten.Ibans = ibans.Distinct(StringComparer.Ordinal).ToList();

            // USt-IdNr. der Quellen erst freigeben, dann übernehmen; Artikel erst nach dem Umhängen der Positionen löschen.
            foreach (var quelle in quellen)
            {
                quelle.UstIdNr = null;
            }

            await db.SaveChangesAsync(ct);
            db.Artikel.RemoveRange(zuLoeschen);
            db.Korrespondenten.RemoveRange(quellen);
            await db.SaveChangesAsync(ct);
            await transaktion.CommitAsync(ct);
            return (await AbrufenAsync(behaltenId, AktuellesJahr, ct))!;
        });
    }

    private int AktuellesJahr => zeit.GetUtcNow().UtcDateTime.Year;

    /// <summary>Hängt die Artikel der Quellen um; gleichnamige werden in den Artikel des behaltenen verschmolzen.</summary>
    /// <returns>Die verschmolzenen Artikel der Quellen, die gelöscht werden.</returns>
    private async Task<List<Artikel>> ArtikelUmhaengenAsync(Korrespondent behalten, List<Guid> quellIds, CancellationToken ct)
    {
        var eigene = await db.Artikel.Where(a => a.KorrespondentId == behalten.Id).ToDictionaryAsync(a => a.NameNormalisiert, ct);
        var fremde = await db.Artikel.Where(a => quellIds.Contains(a.KorrespondentId)).OrderBy(a => a.Name).ToListAsync(ct);
        var fremdIds = fremde.Select(a => a.Id).ToList();
        var positionen = await db.Positionen.Where(p => p.ArtikelId != null && fremdIds.Contains(p.ArtikelId.Value)).ToListAsync(ct);
        var positionenJeArtikel = positionen.ToLookup(p => p.ArtikelId!.Value);

        var zuLoeschen = new List<Artikel>();
        foreach (var artikel in fremde)
        {
            if (eigene.TryGetValue(artikel.NameNormalisiert, out var ziel))
            {
                foreach (var position in positionenJeArtikel[artikel.Id])
                {
                    position.ArtikelId = ziel.Id;
                }

                if (ziel.StammartikelId is null && artikel.StammartikelId is not null)
                {
                    ziel.StammartikelId = artikel.StammartikelId;
                    ziel.UmrechnungFaktor ??= artikel.UmrechnungFaktor;
                }
                else if (ziel.UmrechnungFaktor is null)
                {
                    ziel.UmrechnungFaktor = artikel.UmrechnungFaktor;
                }

                ziel.Einheit ??= artikel.Einheit;
                zuLoeschen.Add(artikel);
            }
            else
            {
                artikel.KorrespondentId = behalten.Id;
                eigene[artikel.NameNormalisiert] = artikel;
            }
        }

        return zuLoeschen;
    }

    private static void FeldFuellen(Korrespondent behalten, Korrespondent quelle)
    {
        behalten.UstIdNr ??= quelle.UstIdNr;
        behalten.Steuernummer ??= quelle.Steuernummer;
        if (AnschriftText(behalten.Anschrift) is null && AnschriftText(quelle.Anschrift) is not null)
        {
            behalten.Anschrift = new Anschrift
            {
                Strasse = quelle.Anschrift.Strasse,
                Plz = quelle.Anschrift.Plz,
                Ort = quelle.Anschrift.Ort,
                Land = quelle.Anschrift.Land,
            };
        }

        if (behalten.Website is null && quelle.Website is not null)
        {
            behalten.Website = quelle.Website;
            behalten.Domain = quelle.Domain;
        }

        behalten.Email ??= quelle.Email;
        if (behalten.LogoPfad is null && quelle.LogoPfad is not null)
        {
            behalten.LogoPfad = quelle.LogoPfad;
            behalten.LogoQuelle = quelle.LogoQuelle;
        }

        behalten.DefaultKategorieId ??= quelle.DefaultKategorieId;
    }

    private async Task UebernehmenAsync(Korrespondent ziel, KorrespondentAendernDto eingabe, CancellationToken ct)
    {
        var fehler = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(eingabe.Name))
        {
            fehler["name"] = ["Der Name ist Pflicht."];
        }

        var ustIdNr = KorrespondentNormalisierung.Kennung(eingabe.UstIdNr);
        if (ustIdNr is not null && !KorrespondentNormalisierung.IstUstIdNr(ustIdNr))
        {
            fehler["ustIdNr"] = ["Die USt-IdNr. hat keine gültige Form."];
        }

        var ibans = (eingabe.Ibans ?? []).Select(KorrespondentNormalisierung.Kennung).OfType<string>().Distinct().ToList();
        var ungueltig = ibans.Where(i => !KorrespondentNormalisierung.IstIban(i)).ToList();
        if (ungueltig.Count > 0)
        {
            fehler["ibans"] = [.. ungueltig.Select(i => $"Die IBAN „{i}“ ist ungültig.")];
        }

        if (eingabe.DefaultKategorieId is { } katId && !await db.Kategorien.AnyAsync(k => k.Id == katId, ct))
        {
            fehler["defaultKategorieId"] = ["Die Kategorie gibt es nicht."];
        }

        if (fehler.Count > 0)
        {
            throw new KorrespondentValidierungsAusnahme(fehler);
        }

        if (ustIdNr is not null)
        {
            var anderer = await db.Korrespondenten
                .Where(k => k.UstIdNr == ustIdNr && k.Id != ziel.Id)
                .Select(k => new { k.Id, k.Name })
                .FirstOrDefaultAsync(ct);
            if (anderer is not null)
            {
                throw new KorrespondentKonfliktAusnahme($"Die USt-IdNr. {ustIdNr} gehört bereits zu „{anderer.Name}“.", anderer.Id);
            }
        }

        var name = eingabe.Name.Trim();
        ziel.Name = name;
        ziel.Aliase = OhneDubletten((eingabe.Aliase ?? []).Where(a => !string.Equals(a.Trim(), name, StringComparison.OrdinalIgnoreCase)));
        ziel.Anschrift = AnschriftAusText(eingabe.Anschrift);
        ziel.UstIdNr = ustIdNr;
        ziel.Steuernummer = Leer(eingabe.Steuernummer);
        ziel.Ibans = ibans;
        ziel.Website = Leer(eingabe.Website);
        ziel.Domain = KorrespondentNormalisierung.Domain(ziel.Website);
        ziel.Email = Leer(eingabe.Email);
        ziel.IstLieferant = eingabe.IstLieferant;
        ziel.IstKunde = eingabe.IstKunde;
        ziel.DefaultKategorieId = eingabe.DefaultKategorieId;
        ziel.Matching = eingabe.Matching is { } m
            ? new Matching { Algorithmus = m.Algorithmus, Muster = Leer(m.Muster), GrossKlein = m.GrossKlein }
            : new Matching();
        ziel.Geprueft = true;
    }

    private static IQueryable<BelegZeile> BelegZeilen(IQueryable<Beleg> belege) =>
        belege.AsNoTracking()
            .Where(b => b.DuplikatVonId == null)
            .Select(b => new BelegZeile(
                b.KorrespondentId,
                b.Rechnungsdatum,
                b.Faelligkeitsdatum,
                b.Brutto,
                b.Waehrung,
                b.Tags.Any(t => t.Tag!.IstZahlungsTag)));

    private async Task<BelegListeneintragDto[]> LetzteBelegeAsync(Korrespondent k, DateOnly heute, CancellationToken ct)
    {
        var belege = await db.Belege.AsNoTracking()
            .Where(b => b.KorrespondentId == k.Id && b.DuplikatVonId == null)
            .OrderByDescending(b => b.Rechnungsdatum)
            .ThenByDescending(b => b.ErstelltAm)
            .Take(AnzahlLetzteBelege)
            .Select(b => new
            {
                b.Id,
                b.Rechnungsnummer,
                b.Titel,
                Kategorie = b.Kategorie == null ? null : new VerweisDto(b.Kategorie.Id, b.Kategorie.Name),
                b.Rechnungsdatum,
                b.Faelligkeitsdatum,
                b.Netto,
                b.Brutto,
                b.Waehrung,
                b.Seitenanzahl,
                b.Quelle,
                Rollen = b.Dateien.Select(d => d.Rolle).ToList(),
                Tags = b.Tags.Select(t => new TagKurzDto(t.Tag!.Id, t.Tag.Name, t.Tag.Farbe, t.Tag.IstInbox, t.Tag.IstZahlungsTag, t.Tag.IstSystem)).ToList(),
                Auszug = b.Volltext == null ? "" : b.Volltext.Substring(0, 200),
                b.DuplikatVonId,
                b.DiKonfidenz,
                b.Leistungsdatum,
            })
            .ToListAsync(ct);

        var verweis = new VerweisDto(k.Id, k.Name);
        var logo = Logo(k);
        return
        [
            .. belege.Select(b =>
            {
                var bezahlt = b.Tags.Any(t => t.IstZahlungsTag);
                return new BelegListeneintragDto(
                    b.Id,
                    b.Rechnungsnummer,
                    verweis,
                    logo,
                    b.Titel ?? b.Rechnungsnummer ?? "",
                    b.Kategorie,
                    b.Rechnungsdatum,
                    b.Faelligkeitsdatum,
                    !bezahlt && b.Faelligkeitsdatum < heute,
                    b.Netto,
                    b.Brutto,
                    b.Waehrung,
                    b.Seitenanzahl,
                    b.Quelle,
                    b.Rollen.Contains(DateiRolle.EingebettetesXml) ? "ZUGFeRD" : "PDF",
                    [.. b.Tags],
                    b.Auszug,
                    b.Rollen.Contains(DateiRolle.Vorschau) ? ApiRouten.Belege.Vorschau.Replace("{id}", b.Id.ToString(), StringComparison.Ordinal) : null,
                    b.DuplikatVonId != null,
                    b.DiKonfidenz,
                    b.Leistungsdatum);
            }),
        ];
    }

    private async Task<(ArtikelListeneintragDto[] Artikel, int Anzahl)> ArtikelAsync(Korrespondent k, CancellationToken ct)
    {
        var artikel = await db.Artikel.AsNoTracking()
            .Where(a => a.KorrespondentId == k.Id)
            .Select(a => new { a.Id, a.Name, a.Einheit, Stammartikel = a.Stammartikel == null ? null : new VerweisDto(a.Stammartikel.Id, a.Stammartikel.Name) })
            .ToListAsync(ct);
        var ids = artikel.Select(a => a.Id).ToList();
        var kaeufe = await db.Positionen.AsNoTracking()
            .Where(p => p.ArtikelId != null && ids.Contains(p.ArtikelId.Value))
            .Join(db.Belege.Where(b => b.DuplikatVonId == null), p => p.BelegId, b => b.Id, (p, b) => new
            {
                ArtikelId = p.ArtikelId!.Value,
                b.Rechnungsdatum,
                p.Einzelpreis,
                p.Einheit,
                p.Grundpreis,
                p.GrundpreisEinheit,
            })
            .ToListAsync(ct);
        var jeArtikel = kaeufe.ToLookup(x => x.ArtikelId);
        var verweis = new VerweisDto(k.Id, k.Name);

        var liste = artikel
            .Select(a =>
            {
                var geordnet = jeArtikel[a.Id].OrderBy(x => x.Rechnungsdatum).ToList();
                var erster = geordnet.FirstOrDefault(x => x.Einzelpreis is not null);
                var letzter = geordnet.LastOrDefault(x => x.Einzelpreis is not null);
                decimal? aenderung = erster?.Einzelpreis is { } alt && alt != 0 && letzter?.Einzelpreis is { } neu
                    ? Math.Round((neu - alt) / alt, 4)
                    : null;
                return new ArtikelListeneintragDto(
                    a.Id,
                    a.Name,
                    verweis,
                    a.Stammartikel,
                    geordnet.Count,
                    letzter?.Einzelpreis,
                    letzter?.Einheit ?? a.Einheit,
                    letzter?.Grundpreis,
                    letzter?.GrundpreisEinheit,
                    aenderung,
                    geordnet.LastOrDefault()?.Rechnungsdatum);
            })
            .Where(a => a.Kaeufe > 0)
            .OrderByDescending(a => a.Kaeufe)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(AnzahlArtikelImDetail)
            .ToArray();
        return (liste, artikel.Count);
    }

    private static bool Trifft(Korrespondent k, string suche)
    {
        var text = suche.Trim();
        if (k.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || k.Aliase.Any(a => a.Contains(text, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var kennung = KorrespondentNormalisierung.Kennung(text);
        return kennung is not null
            && ((k.UstIdNr?.Contains(kennung, StringComparison.Ordinal) ?? false)
                || k.Ibans.Any(i => i.Contains(kennung, StringComparison.Ordinal)));
    }

    private static string? Hinweis(Korrespondent k, Korrespondent? dublette)
    {
        var teile = new List<string>();
        if (k.Aliase.Count > 0)
        {
            teile.Add($"Alias ‚{k.Aliase[0]}‘");
        }

        if (k.Ibans.Count > 1)
        {
            teile.Add($"{k.Ibans.Count} IBANs");
        }

        if (!k.Geprueft)
        {
            teile.Add("noch nicht geprüft");
        }

        if (dublette is not null)
        {
            teile.Add($"vermutlich Dublette von ‚{dublette.Name}‘");
        }

        return teile.Count > 0 ? string.Join(" · ", teile) : null;
    }

    private static LogoDto Logo(Korrespondent k) => new(
        k.LogoPfad is null ? null : ApiRouten.Korrespondenten.Logo.Replace("{id}", k.Id.ToString(), StringComparison.Ordinal),
        Initialen(k.Name),
        k.LogoPfad is null ? LogoQuelle.Keine : k.LogoQuelle);

    private static string Initialen(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsLetterOrDigit(w[0]))
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0])));

    private static MatchingDto MatchingDto(Matching m) => new(m.Algorithmus, m.Muster, m.GrossKlein);

    private static decimal Summe(IEnumerable<BelegZeile> belege) =>
        belege.Where(b => b.Waehrung == "EUR").Sum(b => b.Brutto ?? 0);

    private static List<string> OhneDubletten(IEnumerable<string> werte) =>
        werte.Select(w => w.Trim()).Where(w => w.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();

    /// <summary>Anschrift als Text: Straße, „PLZ Ort“ und Land, je eine Zeile.</summary>
    private static string? AnschriftText(Anschrift a)
    {
        var zeilen = new[] { a.Strasse, $"{a.Plz} {a.Ort}".Trim(), a.Land }.Where(z => !string.IsNullOrWhiteSpace(z)).ToList();
        return zeilen.Count == 0 ? null : string.Join('\n', zeilen);
    }

    private static Anschrift AnschriftAusText(string? text)
    {
        var zeilen = (text ?? "").Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var anschrift = new Anschrift();
        var rest = new List<string>();
        foreach (var zeile in zeilen)
        {
            var leerzeichen = zeile.IndexOf(' ', StringComparison.Ordinal);
            if (anschrift.Plz is null && leerzeichen > 0 && zeile[..leerzeichen].All(char.IsDigit))
            {
                anschrift.Plz = zeile[..leerzeichen];
                anschrift.Ort = zeile[(leerzeichen + 1)..].Trim();
            }
            else if (anschrift.Plz is null && anschrift.Strasse is null)
            {
                anschrift.Strasse = zeile;
            }
            else
            {
                rest.Add(zeile);
            }
        }

        anschrift.Land = rest.Count == 0 ? null : string.Join(", ", rest);
        return anschrift;
    }

    private sealed record BelegZeile(
        Guid? KorrespondentId,
        DateOnly? Rechnungsdatum,
        DateOnly? Faelligkeitsdatum,
        decimal? Brutto,
        string Waehrung,
        bool Bezahlt);
}
