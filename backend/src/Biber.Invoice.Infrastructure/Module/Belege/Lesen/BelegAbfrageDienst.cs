using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.FachlicheDublette.Domain.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.Infrastructure.Persistenz.Konfiguration;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Biber.Invoice.Infrastructure.Module.Belege.Lesen;

/// <summary>
/// Belegliste mit Filtern und Volltextsuche, Zähler der Ansichten, Detail, Nachbarn und ähnliche Belege.
/// Die Liste zeigt nur Belege der <see cref="BelegAbfragen.Grundmenge"/>; das Detail jeden vorhandenen Beleg.
/// </summary>
public sealed partial class BelegAbfrageDienst(BiberDbContext db, IEinstellungen einstellungen, TimeProvider zeit)
    : IBelegAbfrageDienst
{
    public const int MinSeitengroesse = 1;
    public const int MaxSeitengroesse = 200;
    public const int MaxAehnliche = 50;
    private const int MaxAuszug = 300;

    public async Task<BelegListeDto> SuchenAsync(BelegFilterDto filter, CancellationToken ct)
    {
        var heute = Heute();
        var gefiltert = Filtern(filter, heute);
        var seite = Math.Max(1, filter.Seite);
        var groesse = Math.Clamp(filter.Seitengroesse, MinSeitengroesse, MaxSeitengroesse);

        var gesamt = await gefiltert.CountAsync(ct);
        var summe = await gefiltert.OhneDubletten().SumAsync(b => b.Brutto, ct) ?? 0m;
        var zeilen = await Sortieren(gefiltert, filter)
            .Skip((seite - 1) * groesse)
            .Take(groesse)
            .Select(Projektion)
            .ToListAsync(ct);

        return new BelegListeDto([.. zeilen.Select(z => Eintrag(z, heute))], gesamt, seite, groesse, summe);
    }

    public async Task<BelegZaehlerDto> ZaehlerAsync(CancellationToken ct)
    {
        var heute = Heute();
        var basis = db.Belege.AsNoTracking().Grundmenge().OhneDubletten();
        var inbox = basis.NurInbox();
        var zuPruefen = basis.NurZuPruefen();
        var offen = basis.NurOffene();
        var ueberfaellig = basis.NurUeberfaellige(heute);

        // Eine Abfrage: die Zähler laufen als Unterabfragen an einer beliebigen Zeile.
        var zaehler = await db.Belege
            .AsNoTracking()
            .Take(1)
            .Select(_ => new BelegZaehlerDto(
                basis.Count(),
                inbox.Count(),
                zuPruefen.Count(),
                offen.Count(),
                ueberfaellig.Count()))
            .FirstOrDefaultAsync(ct);
        return zaehler ?? new BelegZaehlerDto(0, 0, 0, 0, 0);
    }

    public async Task<BelegDetailDto?> AbrufenAsync(Guid id, CancellationToken ct)
    {
        var beleg = await db.Belege
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Korrespondent).ThenInclude(k => k!.DefaultKategorie)
            .Include(b => b.Steuerzeilen)
            .Include(b => b.Positionen).ThenInclude(p => p.Artikel)
            .Include(b => b.Befunde)
            .Include(b => b.Dateien)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return null;
        }

        var heute = Heute();
        var kopf = Eintrag(await db.Belege.AsNoTracking().Where(b => b.Id == id).Select(Projektion).FirstAsync(ct), heute);
        var toleranz = (await einstellungen.LesenAsync<PruefEinstellungen>(ct)).SummenToleranz;
        var jahre = (await einstellungen.LesenAsync<AufbewahrungEinstellungen>(ct)).Jahre;
        var teilbelege = await db.Belege
            .AsNoTracking()
            .Where(b => b.AufgeteiltAusBelegId == id)
            .OrderBy(b => b.ErstelltAm)
            .ThenBy(b => b.Id)
            .Select(b => b.Id)
            .ToArrayAsync(ct);

        return new BelegDetailDto(
            kopf.Id,
            kopf.Rechnungsnummer,
            kopf.Korrespondent,
            kopf.Logo,
            kopf.Titel,
            kopf.Kategorie,
            kopf.Rechnungsdatum,
            kopf.Faelligkeitsdatum,
            kopf.Ueberfaellig,
            kopf.Netto,
            kopf.Brutto,
            kopf.Waehrung,
            kopf.Seitenanzahl,
            kopf.Quelle,
            kopf.Format,
            kopf.Tags,
            kopf.Auszug,
            kopf.VorschauUrl,
            kopf.IstDublette,
            kopf.Konfidenz,
            kopf.Leistungsdatum,
            beleg.Belegart,
            beleg.Status,
            beleg.KorrespondentErkennung,
            [.. beleg.Bankverbindungen.Select(b => new BankverbindungDto(b.Iban, b.Bic))],
            beleg.KategorieManuell,
            beleg.Korrespondent?.DefaultKategorie is { } standard ? new VerweisDto(standard.Id, standard.Name) : null,
            beleg.LeistungVon,
            beleg.LeistungBis,
            beleg.Steuer,
            [.. beleg.Steuerzeilen
                .OrderByDescending(s => s.Satz)
                .Select(s => new SteuerzeileDto(s.Satz, s.Netto, s.Steuer, SteuerStimmt(s, toleranz)))],
            [.. beleg.Positionen
                .OrderBy(p => p.Nr)
                .Select(p => new PositionDto(
                    p.Nr,
                    p.Text,
                    p.Menge,
                    p.Einheit,
                    p.Einzelpreis,
                    p.Gesamt ?? 0m,
                    p.Steuersatz,
                    p.Artikel is null ? null : new VerweisDto(p.Artikel.Id, p.Artikel.Name),
                    p.Artikel?.StammartikelId,
                    p.Grundpreis,
                    p.GrundpreisEinheit))],
            [.. beleg.Befunde
                .OrderBy(f => f.Ergebnis switch
                {
                    PruefErgebnis.Fehlgeschlagen => 0,
                    PruefErgebnis.Ok => 1,
                    _ => 2,
                })
                .ThenBy(f => f.Pruefung)
                .Select(f => new PruefBefundDto(f.Pruefung, PruefungName(f.Pruefung), f.Ergebnis, f.Detail))],
            [.. beleg.Dateien
                .OrderBy(d => d.Rolle)
                .ThenBy(d => d.AbgelegtAm)
                .Select(d => new BelegDateiDto(
                    d.Id,
                    d.Rolle,
                    d.MimeType,
                    d.Pfad,
                    d.Sha256,
                    d.Groesse,
                    d.AbgelegtAm,
                    d.LetztePruefungAm,
                    d.PruefErgebnis,
                    DateiUrl(id, d.Id)))],
            new ExtraktionDto(beleg.ExtraktionsQuelle, beleg.DiKonfidenz, beleg.ExtrahiertAm, beleg.Seitenanzahl),
            await DublettenAsync(beleg, heute, ct),
            beleg.LoeschsperreBis(jahre),
            IstERechnung(beleg.ExtraktionsQuelle),
            beleg.AufgeteiltAusBelegId,
            teilbelege);
    }

    public async Task<BelegNachbarnDto> NachbarnAsync(Guid id, BelegFilterDto filter, CancellationToken ct)
    {
        await SicherstellenAsync(id, ct);
        var ids = await Sortieren(Filtern(filter, Heute()), filter).Select(b => b.Id).ToListAsync(ct);
        var index = ids.IndexOf(id);
        if (index < 0)
        {
            return new BelegNachbarnDto(null, null, 0, ids.Count);
        }

        return new BelegNachbarnDto(
            index > 0 ? ids[index - 1] : null,
            index < ids.Count - 1 ? ids[index + 1] : null,
            index + 1,
            ids.Count);
    }

    /// <summary>
    /// Belege desselben Korrespondenten oder mit mindestens drei der zehn häufigsten Begriffe des Belegs (bei
    /// weniger Begriffen alle); ein einzelner gemeinsamer Allerweltsbegriff reicht nicht.
    /// Rang = gleicher Korrespondent (1) + <c>ts_rank</c> gegen diese Begriffe.
    /// </summary>
    public async Task<IReadOnlyList<BelegListeneintragDto>> AehnlicheAsync(Guid id, int anzahl, CancellationToken ct)
    {
        var korrespondentId = await db.Belege.Where(b => b.Id == id).Select(b => b.KorrespondentId).ToListAsync(ct) switch
        {
            [] => throw new BelegNichtGefundenAusnahme(id),
            [var k] => k ?? Guid.Empty,
            _ => Guid.Empty,
        };
        anzahl = Math.Clamp(anzahl, 1, MaxAehnliche);

        var ids = await db.Database.SqlQuery<Guid>($"""
            WITH begriffe AS (
                SELECT u.lexeme
                FROM beleg b, unnest(b.suchvektor) AS u
                WHERE b.id = {id}
                ORDER BY coalesce(cardinality(u.positions), 0) DESC, u.lexeme
                LIMIT 10),
            anfrage AS (
                SELECT coalesce(array_agg(lexeme), ARRAY[]::text[]) AS liste,
                       to_tsquery('simple', string_agg(quote_literal(lexeme), ' | ')) AS q
                FROM begriffe),
            kandidaten AS (
                SELECT b.id,
                       b.erstellt_am,
                       coalesce(b.korrespondent_id = {korrespondentId}, false) AS gleicher,
                       (SELECT count(*) FROM unnest(tsvector_to_array(b.suchvektor)) AS l WHERE l = ANY(a.liste)) AS gemeinsam,
                       cardinality(a.liste) AS begriffe,
                       coalesce(ts_rank(b.suchvektor, a.q), 0) AS rang
                FROM beleg b CROSS JOIN anfrage a
                WHERE b.id <> {id} AND b.status IN ('Fertig', 'Fehler'))
            SELECT id AS "Value"
            FROM kandidaten
            WHERE gleicher OR (gemeinsam > 0 AND gemeinsam >= least(3, begriffe))
            ORDER BY (CASE WHEN gleicher THEN 1 ELSE 0 END) + rang DESC, erstellt_am DESC, id
            LIMIT {anzahl}
            """).ToListAsync(ct);

        var heute = Heute();
        var zeilen = await db.Belege.AsNoTracking().Where(b => ids.Contains(b.Id)).Select(Projektion).ToListAsync(ct);
        return [.. zeilen.OrderBy(z => ids.IndexOf(z.Id)).Select(z => Eintrag(z, heute))];
    }

    /// <summary>Anzeigename einer Prüfung wie in der Detailansicht.</summary>
    public static string PruefungName(PruefungArt art) => art switch
    {
        PruefungArt.Positionssumme => "Summe der Positionen = Netto",
        PruefungArt.NettoSteuerBrutto => "Netto + USt = Brutto je Steuersatz",
        PruefungArt.Pflichtfelder => "Pflichtfelder",
        PruefungArt.Datum => "Datum plausibel",
        PruefungArt.Paragraph14 => "Angaben nach § 14 UStG",
        PruefungArt.Dublette => "Dublettenverdacht",
        PruefungArt.NeuerKorrespondent => "Neuer Korrespondent",
        PruefungArt.Konfidenz => "Erkennungssicherheit",
        PruefungArt.Kosit => "KoSIT-Validierung",
        PruefungArt.Bankverbindung => "Bankverbindung bekannt",
        _ => art.ToString(),
    };

    /// <summary>„XRechnung“/„ZUGFeRD“ nach der Extraktion, sonst „Scan“ aus dem Eingangsordner und „PDF“.</summary>
    public static string Format(ExtraktionsQuelle extraktion, ImportQuelle quelle) => extraktion switch
    {
        ExtraktionsQuelle.XRechnung => "XRechnung",
        ExtraktionsQuelle.ZUGFeRD => "ZUGFeRD",
        _ => quelle == ImportQuelle.Ordner ? "Scan" : "PDF",
    };

    /// <summary>Erste 300 Zeichen des Volltexts, Zeilenumbrüche als „ · “.</summary>
    public static string Auszug(string? volltext)
    {
        if (string.IsNullOrEmpty(volltext))
        {
            return "";
        }

        var anfang = volltext.Length <= MaxAuszug ? volltext : volltext[..MaxAuszug];
        return string.Join(" · ", ZeilenumbruchRegex().Split(anfang).Select(z => z.Trim()).Where(z => z.Length > 0));
    }

    /// <summary>Anfangsbuchstaben der ersten zwei Wörter, „?“ ohne Namen.</summary>
    public static string Initialen(string? name)
    {
        var initialen = string.Concat((name ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0])));
        return initialen.Length == 0 ? "?" : initialen;
    }

    private static bool SteuerStimmt(Steuerzeile zeile, decimal toleranz) =>
        Math.Abs((zeile.Netto * zeile.Satz / 100m) - zeile.Steuer) <= toleranz;

    private static bool IstERechnung(ExtraktionsQuelle quelle) =>
        quelle is ExtraktionsQuelle.XRechnung or ExtraktionsQuelle.ZUGFeRD;

    private static string DateiUrl(Guid belegId, Guid dateiId) => ApiRouten.Belege.Datei
        .Replace("{id}", belegId.ToString(), StringComparison.Ordinal)
        .Replace("{dateiId}", dateiId.ToString(), StringComparison.Ordinal);

    private async Task<DubletteDto> DublettenAsync(Beleg beleg, DateOnly heute, CancellationToken ct)
    {
        VerweisDto? duplikatVon = null;
        if (beleg.DuplikatVonId is { } originalId)
        {
            duplikatVon = await db.Belege
                .AsNoTracking()
                .Where(b => b.Id == originalId)
                .Select(b => new { b.Id, b.Titel, b.Rechnungsnummer, Korrespondent = b.Korrespondent!.Name })
                .Select(b => new VerweisDto(b.Id, b.Titel ?? b.Rechnungsnummer ?? b.Korrespondent ?? ""))
                .FirstOrDefaultAsync(ct);
        }

        var duplikate = await db.Belege
            .AsNoTracking()
            .Where(b => b.DuplikatVonId == beleg.Id)
            .OrderBy(b => b.ErstelltAm)
            .ThenBy(b => b.Id)
            .Select(Projektion)
            .ToListAsync(ct);

        var hashes = beleg.Dateien.Where(d => d.Rolle == DateiRolle.Original).Select(d => d.Sha256).ToList();
        var hashTreffer = hashes.Count > 0 && await db.BelegDateien.AnyAsync(
            d => d.BelegId != beleg.Id && d.Rolle == DateiRolle.Original && hashes.Contains(d.Sha256), ct);

        return new DubletteDto(
            duplikatVon,
            [.. duplikate.Select(z => Eintrag(z, heute))],
            hashTreffer,
            await FachlicherTrefferAsync(beleg, ct));
    }

    /// <summary>Gleicher Korrespondent, gleicher Bruttobetrag und gleiche normalisierte Rechnungsnummer bei einem anderen Beleg.</summary>
    private async Task<bool> FachlicherTrefferAsync(Beleg beleg, CancellationToken ct)
    {
        var nummer = RechnungsnummerNormalisierer.Normalisieren(beleg.Rechnungsnummer);
        if (beleg.KorrespondentId is not { } korrespondentId || nummer is null || beleg.Brutto is not { } brutto)
        {
            return false;
        }

        var nummern = await db.Belege
            .AsNoTracking()
            .Where(b => b.Id != beleg.Id && b.KorrespondentId == korrespondentId && b.Brutto == brutto && b.Rechnungsnummer != null)
            .Select(b => b.Rechnungsnummer)
            .ToListAsync(ct);
        return nummern.Any(n => RechnungsnummerNormalisierer.Normalisieren(n) == nummer);
    }

    private async Task SicherstellenAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Belege.AnyAsync(b => b.Id == id, ct))
        {
            throw new BelegNichtGefundenAusnahme(id);
        }
    }

    private DateOnly Heute() => DateOnly.FromDateTime(zeit.GetUtcNow().UtcDateTime);

    private IQueryable<Beleg> Filtern(BelegFilterDto filter, DateOnly heute)
    {
        var abfrage = db.Belege.AsNoTracking().Grundmenge().MitAnsicht(filter.Ansicht, heute);
        abfrage = Suchen(abfrage, filter.Suche, filter.Suchfeld);

        foreach (var tagId in filter.TagIds.Distinct())
        {
            abfrage = abfrage.Where(b => b.Tags.Any(t => t.TagId == tagId));
        }

        if (filter.KorrespondentIds.Length > 0)
        {
            var korrespondentIds = filter.KorrespondentIds;
            abfrage = abfrage.Where(b => b.KorrespondentId != null && korrespondentIds.Contains(b.KorrespondentId.Value));
        }

        if (filter.KategorieIds.Length > 0)
        {
            var kategorieIds = filter.KategorieIds;
            abfrage = abfrage.Where(b => b.KategorieId != null && kategorieIds.Contains(b.KategorieId.Value));
        }

        if (filter.Von is { } von)
        {
            abfrage = abfrage.Where(b => b.Rechnungsdatum >= von);
        }

        if (filter.Bis is { } bis)
        {
            abfrage = abfrage.Where(b => b.Rechnungsdatum <= bis);
        }

        if (filter.BetragVon is { } betragVon)
        {
            abfrage = filter.BetragArt == BetragArt.Netto
                ? abfrage.Where(b => b.Netto >= betragVon)
                : abfrage.Where(b => b.Brutto >= betragVon);
        }

        if (filter.BetragBis is { } betragBis)
        {
            abfrage = filter.BetragArt == BetragArt.Netto
                ? abfrage.Where(b => b.Netto <= betragBis)
                : abfrage.Where(b => b.Brutto <= betragBis);
        }

        abfrage = abfrage.MitZahlstatus(filter.Zahlstatus, heute);

        if (filter.Quellen.Length > 0)
        {
            var quellen = filter.Quellen;
            abfrage = abfrage.Where(b => quellen.Contains(b.Quelle));
        }

        return filter.DublettenAusschliessen ? abfrage.OhneDubletten() : abfrage;
    }

    /// <summary>
    /// Titel/Inhalt: Volltext (<c>websearch_to_tsquery</c>, letztes Wort als Präfix) oder Teiltreffer in
    /// Rechnungsnummer bzw. Korrespondentenname (Trigramm-Index); Rechnungsnummer: nur Teiltreffer dort.
    /// </summary>
    private static IQueryable<Beleg> Suchen(IQueryable<Beleg> abfrage, string? suche, BelegSuchfeld feld)
    {
        var begriff = Bereinigen(suche);
        if (begriff.Length == 0)
        {
            return abfrage;
        }

        var muster = $"%{LikeMaskieren(begriff)}%";
        if (feld == BelegSuchfeld.Rechnungsnummer)
        {
            return abfrage.Where(b => EF.Functions.ILike(b.Rechnungsnummer!, muster));
        }

        var woerter = WortRegex().Matches(begriff).Select(m => m.Value).ToArray();
        var praefixText = string.Join(" & ", woerter.Select((w, i) => i == woerter.Length - 1 ? $"{w}:*" : w));
        var hatWoerter = woerter.Length > 0;
        return abfrage.Where(b =>
            EF.Property<NpgsqlTsVector>(b, BelegKonfiguration.Suchvektor).Matches(EF.Functions.WebSearchToTsQuery("german", begriff))
            || (hatWoerter && EF.Property<NpgsqlTsVector>(b, BelegKonfiguration.Suchvektor).Matches(EF.Functions.ToTsQuery("german", praefixText)))
            || EF.Functions.ILike(b.Rechnungsnummer!, muster)
            || (b.Korrespondent != null
                && (EF.Functions.ILike(b.Korrespondent.Name, muster) || EF.Functions.TrigramsAreSimilar(b.Korrespondent.Name, begriff))));
    }

    /// <summary>Sortiert nach dem gewählten Feld, bei Gleichstand nach Erstellung (neueste zuerst) und ID.</summary>
    private static IQueryable<Beleg> Sortieren(IQueryable<Beleg> abfrage, BelegFilterDto filter)
    {
        var ab = filter.Absteigend;
        IOrderedQueryable<Beleg> sortiert = filter.Sortierung switch
        {
            BelegSortierung.Faelligkeit => Ordnen(abfrage, b => b.Faelligkeitsdatum, ab),
            BelegSortierung.Brutto => Ordnen(abfrage, b => b.Brutto, ab),
            BelegSortierung.Korrespondent => Ordnen(abfrage, b => b.Korrespondent!.Name, ab),
            BelegSortierung.ErstelltAm => Ordnen(abfrage, b => b.ErstelltAm, ab),
            BelegSortierung.Rechnungsnummer => Ordnen(abfrage, b => b.Rechnungsnummer, ab),
            BelegSortierung.Titel => Ordnen(abfrage, b => b.Titel ?? b.Rechnungsnummer ?? b.Korrespondent!.Name, ab),
            BelegSortierung.Kategorie => Ordnen(abfrage, b => b.Kategorie!.Name, ab),
            BelegSortierung.Netto => Ordnen(abfrage, b => b.Netto, ab),
            BelegSortierung.Seiten => Ordnen(abfrage, b => b.Seitenanzahl, ab),
            BelegSortierung.Herkunft => Ordnen(abfrage, b => b.Quelle, ab),
            BelegSortierung.Leistungsdatum => Ordnen(abfrage, b => b.Leistungsdatum, ab),
            BelegSortierung.Konfidenz => Ordnen(abfrage, b => b.DiKonfidenz, ab),
            _ => Ordnen(abfrage, b => b.Rechnungsdatum, ab),
        };
        return sortiert.ThenByDescending(b => b.ErstelltAm).ThenBy(b => b.Id);
    }

    private static IOrderedQueryable<Beleg> Ordnen<T>(IQueryable<Beleg> abfrage, Expression<Func<Beleg, T>> feld, bool absteigend) =>
        absteigend ? abfrage.OrderByDescending(feld) : abfrage.OrderBy(feld);

    private static readonly Expression<Func<Beleg, ListenZeile>> Projektion = b => new ListenZeile
    {
        Id = b.Id,
        Rechnungsnummer = b.Rechnungsnummer,
        Titel = b.Titel,
        Korrespondent = b.Korrespondent == null
            ? null
            : new KorrespondentZeile(b.Korrespondent.Id, b.Korrespondent.Name, b.Korrespondent.LogoPfad, b.Korrespondent.LogoQuelle),
        Kategorie = b.Kategorie == null ? null : new VerweisDto(b.Kategorie.Id, b.Kategorie.Name),
        Rechnungsdatum = b.Rechnungsdatum,
        Faelligkeitsdatum = b.Faelligkeitsdatum,
        Netto = b.Netto,
        Brutto = b.Brutto,
        Waehrung = b.Waehrung,
        Seitenanzahl = b.Seitenanzahl,
        Quelle = b.Quelle,
        ExtraktionsQuelle = b.ExtraktionsQuelle,
        Tags = b.Tags
            .OrderBy(t => t.Tag!.Name)
            .Select(t => new TagKurzDto(t.Tag!.Id, t.Tag.Name, t.Tag.Farbe, t.Tag.IstInbox, t.Tag.IstZahlungsTag, t.Tag.IstSystem))
            .ToList(),
        VolltextAnfang = b.Volltext == null ? null : b.Volltext.Substring(0, MaxAuszug),
        HatVorschau = b.Dateien.Any(d => d.Rolle == DateiRolle.Vorschau),
        DuplikatVonId = b.DuplikatVonId,
        Konfidenz = b.DiKonfidenz,
        Leistungsdatum = b.Leistungsdatum,
    };

    private static BelegListeneintragDto Eintrag(ListenZeile z, DateOnly heute)
    {
        var tags = z.Tags.ToArray();
        var offen = z.DuplikatVonId is null && !tags.Any(t => t.IstZahlungsTag);
        var k = z.Korrespondent;
        return new BelegListeneintragDto(
            z.Id,
            z.Rechnungsnummer,
            k is null ? null : new VerweisDto(k.Id, k.Name),
            k is null
                ? new LogoDto(null, "?", LogoQuelle.Keine)
                : new LogoDto(
                    k.LogoPfad is null ? null : ApiRouten.Korrespondenten.Logo.Replace("{id}", k.Id.ToString(), StringComparison.Ordinal),
                    Initialen(k.Name),
                    k.LogoPfad is null ? LogoQuelle.Keine : k.LogoQuelle),
            z.Titel ?? z.Rechnungsnummer ?? k?.Name ?? "",
            z.Kategorie,
            z.Rechnungsdatum,
            z.Faelligkeitsdatum,
            offen && z.Faelligkeitsdatum < heute,
            z.Netto,
            z.Brutto,
            z.Waehrung,
            z.Seitenanzahl,
            z.Quelle,
            Format(z.ExtraktionsQuelle, z.Quelle),
            tags,
            Auszug(z.VolltextAnfang),
            z.HatVorschau ? ApiRouten.Belege.Vorschau.Replace("{id}", z.Id.ToString(), StringComparison.Ordinal) : null,
            z.DuplikatVonId is not null,
            z.Konfidenz,
            z.Leistungsdatum);
    }

    /// <summary>Trimmt und entfernt Steuerzeichen (Postgres verträgt kein NUL in Text).</summary>
    private static string Bereinigen(string? text) =>
        text is null ? "" : new string([.. text.Where(c => !char.IsControl(c))]).Trim();

    private static string LikeMaskieren(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WortRegex();

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex ZeilenumbruchRegex();

    private sealed record KorrespondentZeile(Guid Id, string Name, string? LogoPfad, LogoQuelle LogoQuelle);

    private sealed class ListenZeile
    {
        public Guid Id { get; init; }
        public string? Rechnungsnummer { get; init; }
        public string? Titel { get; init; }
        public KorrespondentZeile? Korrespondent { get; init; }
        public VerweisDto? Kategorie { get; init; }
        public DateOnly? Rechnungsdatum { get; init; }
        public DateOnly? Faelligkeitsdatum { get; init; }
        public decimal? Netto { get; init; }
        public decimal? Brutto { get; init; }
        public string Waehrung { get; init; } = "EUR";
        public int Seitenanzahl { get; init; }
        public ImportQuelle Quelle { get; init; }
        public ExtraktionsQuelle ExtraktionsQuelle { get; init; }
        public List<TagKurzDto> Tags { get; init; } = [];
        public string? VolltextAnfang { get; init; }
        public bool HatVorschau { get; init; }
        public Guid? DuplikatVonId { get; init; }
        public decimal? Konfidenz { get; init; }
        public DateOnly? Leistungsdatum { get; init; }
    }
}

/// <summary>Der angefragte Beleg existiert nicht; die API antwortet mit 404.</summary>
public sealed class BelegNichtGefundenAusnahme(Guid id) : Exception($"Beleg {id} nicht gefunden.");
