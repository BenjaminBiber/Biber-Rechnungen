using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Auswertung;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Auswertung;

/// <summary>
/// Dashboard und Kostenauswertung. Kosten sind brutto in EUR nach Rechnungsdatum, nur fertige
/// Eingangsbelege ohne Dubletten; Gutschriften zählen negativ.
/// </summary>
public sealed class AuswertungsDienst(BiberDbContext db, IEinstellungen einstellungen, TimeProvider zeit) : IAuswertungsDienst
{
    public const string ZeitraumMonat = "Monat";
    public const string ZeitraumQuartal = "Quartal";
    public const string ZeitraumJahr = "Jahr";

    public const string OhneKategorie = "Ohne Kategorie";
    public const string OhneKategorieFarbe = "#9AA1AD";
    public const string OhneKorrespondent = "Ohne Korrespondent";

    public static readonly IReadOnlyList<string> Zeitraeume = [ZeitraumMonat, ZeitraumQuartal, ZeitraumJahr];

    private const int AnzahlTopKorrespondenten = 5;
    private const int AnzahlZuPruefen = 5;
    private const int AnzahlOffeneBelege = 4;
    private const int AnzahlSkonto = 4;
    private const int AnzahlPreisaenderungen = 5;

    private static readonly string[] Monatsnamen =
        ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    private static readonly string[] Monatskurz =
        ["Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"];

    /// <param name="jahr">Jahr der Auswertung; <c>0</c> oder kleiner steht für das laufende Jahr.</param>
    public async Task<DashboardDto> DashboardAsync(string zeitraum, int jahr, CancellationToken ct)
    {
        var heute = await HeuteAsync(ct);
        if (jahr <= 0)
        {
            jahr = heute.Year;
        }

        // Stichtag: heute im laufenden Jahr, sonst das Ende (vergangen) bzw. der Anfang (künftig) des Jahres.
        var stichtag = jahr == heute.Year ? heute
            : jahr < heute.Year ? new DateOnly(jahr, 12, 31)
            : new DateOnly(jahr, 1, 1);

        var tage = await TagesSummenAsync(new DateOnly(jahr - 1, 1, 1), new DateOnly(jahr, 12, 31), ct);
        var kategorien = await db.Kategorien.AsNoTracking().ToDictionaryAsync(k => k.Id, ct);

        return new DashboardDto(
            Kosten: KostenKennzahl(tage, zeitraum, stichtag),
            KostenJahr: KostenJahrKennzahl(tage, stichtag),
            Offen: await OffenAsync(heute, nurUeberfaellige: false, ct),
            Ueberfaellig: await OffenAsync(heute, nurUeberfaellige: true, ct),
            Inbox: Anzahl(await FertigeBelege().CountAsync(b => b.Tags.Any(t => t.Tag!.IstInbox), ct)),
            ZuPruefen: Anzahl(await FertigeBelege().CountAsync(b => b.Tags.Any(t => t.TagId == SystemIds.TagZuPruefen), ct)),
            KostenProMonat: KostenProMonat(tage, jahr, kategorien),
            TopKorrespondenten: await TopKorrespondentenAsync(jahr, ct),
            ZuPruefenListe: await ZuPruefenListeAsync(ct),
            OffeneBelege: await OffeneBelegeAsync(heute, ct),
            Skonto: await SkontoAsync(heute, ct),
            Preisaenderungen: await PreisaenderungenAsync(jahr, ct),
            Vorsteuer: await VorsteuerAsync(tage, jahr, ct),
            DiVerbrauch: await DiVerbrauchAsync(heute, ct));
    }

    public async Task<KostenAuswertungDto> KostenAsync(DateOnly von, DateOnly bis, KostenGruppierung gruppierung, CancellationToken ct)
    {
        var werte = KostenBelege()
            .Where(b => b.Rechnungsdatum >= von && b.Rechnungsdatum <= bis)
            .Select(b => new
            {
                b.Rechnungsdatum!.Value.Year,
                b.Rechnungsdatum.Value.Month,
                b.KategorieId,
                b.KorrespondentId,
                Netto = b.Belegart == Belegart.Gutschrift ? -Math.Abs(b.Netto ?? 0) : b.Netto ?? 0,
                Steuer = b.Belegart == Belegart.Gutschrift ? -Math.Abs(b.Steuer ?? 0) : b.Steuer ?? 0,
                Brutto = b.Belegart == Belegart.Gutschrift ? -Math.Abs(b.Brutto!.Value) : b.Brutto!.Value,
            });

        List<KostenZeileDto> zeilen;
        switch (gruppierung)
        {
            case KostenGruppierung.Monat:
                var monate = await werte
                    .GroupBy(w => new { w.Year, w.Month })
                    .Select(g => new { g.Key.Year, g.Key.Month, Netto = g.Sum(w => w.Netto), Steuer = g.Sum(w => w.Steuer), Brutto = g.Sum(w => w.Brutto), Anzahl = g.Count() })
                    .ToListAsync(ct);
                zeilen = monate
                    .OrderBy(m => m.Year).ThenBy(m => m.Month)
                    .Select(m => new KostenZeileDto($"{m.Year:D4}-{m.Month:D2}", $"{Monatsnamen[m.Month - 1]} {m.Year}", m.Netto, m.Steuer, m.Brutto, m.Anzahl))
                    .ToList();
                break;

            case KostenGruppierung.Kategorie:
                var jeKategorie = await werte
                    .GroupBy(w => w.KategorieId)
                    .Select(g => new { Id = g.Key, Netto = g.Sum(w => w.Netto), Steuer = g.Sum(w => w.Steuer), Brutto = g.Sum(w => w.Brutto), Anzahl = g.Count() })
                    .ToListAsync(ct);
                var kategorien = await db.Kategorien.AsNoTracking().ToDictionaryAsync(k => k.Id, k => k.Name, ct);
                zeilen = jeKategorie
                    .Select(k => new KostenZeileDto(
                        k.Id?.ToString() ?? "ohne",
                        k.Id is { } id && kategorien.TryGetValue(id, out var name) ? name : OhneKategorie,
                        k.Netto, k.Steuer, k.Brutto, k.Anzahl))
                    .OrderByDescending(z => z.Brutto).ThenBy(z => z.Bezeichnung, StringComparer.CurrentCulture)
                    .ToList();
                break;

            case KostenGruppierung.Korrespondent:
                var jeKorrespondent = await werte
                    .GroupBy(w => w.KorrespondentId)
                    .Select(g => new { Id = g.Key, Netto = g.Sum(w => w.Netto), Steuer = g.Sum(w => w.Steuer), Brutto = g.Sum(w => w.Brutto), Anzahl = g.Count() })
                    .ToListAsync(ct);
                var ids = jeKorrespondent.Where(k => k.Id != null).Select(k => k.Id!.Value).ToList();
                var korrespondenten = await db.Korrespondenten.AsNoTracking()
                    .Where(k => ids.Contains(k.Id))
                    .ToDictionaryAsync(k => k.Id, k => k.Name, ct);
                zeilen = jeKorrespondent
                    .Select(k => new KostenZeileDto(
                        k.Id?.ToString() ?? "ohne",
                        k.Id is { } id && korrespondenten.TryGetValue(id, out var name) ? name : OhneKorrespondent,
                        k.Netto, k.Steuer, k.Brutto, k.Anzahl))
                    .OrderByDescending(z => z.Brutto).ThenBy(z => z.Bezeichnung, StringComparer.CurrentCulture)
                    .ToList();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(gruppierung), gruppierung, null);
        }

        return new KostenAuswertungDto([.. zeilen], zeilen.Sum(z => z.Brutto));
    }

    /// <summary>Fertige Belege ohne Dubletten.</summary>
    private IQueryable<Beleg> FertigeBelege() =>
        db.Belege.AsNoTracking().Where(b => b.Status == BelegStatus.Fertig && b.DuplikatVonId == null);

    /// <summary>Belege, die in Kostenzahlen eingehen: fertige Eingangsbelege in EUR mit Betrag und Rechnungsdatum.</summary>
    private IQueryable<Beleg> KostenBelege() =>
        FertigeBelege().Where(b =>
            b.Richtung == Richtung.Eingang
            && b.Waehrung == "EUR"
            && b.Brutto != null
            && b.Rechnungsdatum != null);

    private async Task<DateOnly> HeuteAsync(CancellationToken ct)
    {
        var allgemein = await einstellungen.LesenAsync<AllgemeinEinstellungen>(ct);
        var jetzt = zeit.GetUtcNow();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(allgemein.Zeitzone, out var zone))
        {
            jetzt = TimeZoneInfo.ConvertTime(jetzt, zone);
        }

        return DateOnly.FromDateTime(jetzt.DateTime);
    }

    /// <summary>Summen je Rechnungsdatum und Kategorie – Grundlage für Kacheln, Monatswerte und Vorsteuer.</summary>
    private async Task<List<TagesSumme>> TagesSummenAsync(DateOnly von, DateOnly bis, CancellationToken ct) =>
        await KostenBelege()
            .Where(b => b.Rechnungsdatum >= von && b.Rechnungsdatum <= bis)
            .Select(b => new
            {
                Datum = b.Rechnungsdatum!.Value,
                b.KategorieId,
                Brutto = b.Belegart == Belegart.Gutschrift ? -Math.Abs(b.Brutto!.Value) : b.Brutto!.Value,
                Steuer = b.Belegart == Belegart.Gutschrift ? -Math.Abs(b.Steuer ?? 0) : b.Steuer ?? 0,
            })
            .GroupBy(b => new { b.Datum, b.KategorieId })
            .Select(g => new TagesSumme(g.Key.Datum, g.Key.KategorieId, g.Sum(b => b.Brutto), g.Sum(b => b.Steuer), g.Count()))
            .ToListAsync(ct);

    private static DashboardKennzahlDto KostenKennzahl(List<TagesSumme> tage, string zeitraum, DateOnly stichtag)
    {
        var (von, bis, vergleich) = zeitraum switch
        {
            ZeitraumQuartal => Quartal(stichtag),
            ZeitraumJahr => (new DateOnly(stichtag.Year, 1, 1), new DateOnly(stichtag.Year, 12, 31), $"{stichtag.Year - 1}"),
            _ => (new DateOnly(stichtag.Year, stichtag.Month, 1), new DateOnly(stichtag.Year, stichtag.Month, 1).AddMonths(1).AddDays(-1),
                $"{Monatskurz[stichtag.Month - 1]} {stichtag.Year - 1}"),
        };
        return Vergleich(tage, von, bis, vergleich);
    }

    private static (DateOnly Von, DateOnly Bis, string Vergleich) Quartal(DateOnly stichtag)
    {
        var nummer = (stichtag.Month - 1) / 3 + 1;
        var von = new DateOnly(stichtag.Year, (nummer - 1) * 3 + 1, 1);
        return (von, von.AddMonths(3).AddDays(-1), $"Q{nummer} {stichtag.Year - 1}");
    }

    /// <summary>Jahr bis zum Stichtag, verglichen mit demselben Abschnitt des Vorjahres.</summary>
    private static DashboardKennzahlDto KostenJahrKennzahl(List<TagesSumme> tage, DateOnly stichtag)
    {
        var bereich = stichtag.Month == 1 ? "Jan" : $"Jan–{Monatskurz[stichtag.Month - 1]}";
        return Vergleich(tage, new DateOnly(stichtag.Year, 1, 1), stichtag, $"{bereich} {stichtag.Year - 1}");
    }

    private static DashboardKennzahlDto Vergleich(List<TagesSumme> tage, DateOnly von, DateOnly bis, string vergleichszeitraum)
    {
        var aktuell = tage.Where(t => t.Datum >= von && t.Datum <= bis).ToList();
        var vorjahr = tage.Where(t => t.Datum >= von.AddYears(-1) && t.Datum <= bis.AddYears(-1)).Sum(t => t.Brutto);
        var summe = aktuell.Sum(t => t.Brutto);
        decimal? aenderung = vorjahr == 0 ? null : Math.Round((summe - vorjahr) / Math.Abs(vorjahr) * 100, 1, MidpointRounding.AwayFromZero);
        return new DashboardKennzahlDto(aktuell.Sum(t => t.Anzahl), summe, aenderung, vergleichszeitraum, null);
    }

    private static DashboardKennzahlDto Anzahl(int anzahl) => new(anzahl, null, null, null, null);

    /// <summary>Offen = Rechnung ohne Zahlungs-Tag; überfällig = zusätzlich Fälligkeit vor heute. Über alle Jahre.</summary>
    private async Task<DashboardKennzahlDto> OffenAsync(DateOnly heute, bool nurUeberfaellige, CancellationToken ct)
    {
        var offen = OffeneRechnungen();
        if (nurUeberfaellige)
        {
            offen = offen.Where(b => b.Faelligkeitsdatum != null && b.Faelligkeitsdatum < heute);
        }

        var werte = await offen
            .GroupBy(_ => 1)
            .Select(g => new { Anzahl = g.Count(), Summe = g.Sum(b => b.Brutto!.Value), Aelteste = g.Min(b => b.Faelligkeitsdatum) })
            .FirstOrDefaultAsync(ct);
        if (werte is null)
        {
            return new DashboardKennzahlDto(0, 0, null, null, nurUeberfaellige ? 0 : null);
        }

        int? aeltesteTage = nurUeberfaellige && werte.Aelteste is { } aelteste ? heute.DayNumber - aelteste.DayNumber : null;
        return new DashboardKennzahlDto(werte.Anzahl, werte.Summe, null, null, aeltesteTage);
    }

    private IQueryable<Beleg> OffeneRechnungen() =>
        KostenBelege().Where(b => b.Belegart == Belegart.Rechnung && !b.Tags.Any(t => t.Tag!.IstZahlungsTag));

    private static MonatsKostenDto[] KostenProMonat(List<TagesSumme> tage, int jahr, Dictionary<Guid, Kategorie> kategorien) =>
        Enumerable.Range(1, 12).Select(monat =>
        {
            var imMonat = tage.Where(t => t.Datum.Year == jahr && t.Datum.Month == monat).ToList();
            var nachKategorie = imMonat
                .GroupBy(t => t.KategorieId is { } id && kategorien.ContainsKey(id) ? id : (Guid?)null)
                .Select(g => g.Key is { } id
                    ? new KategorieAnteilDto(new VerweisDto(id, kategorien[id].Name), kategorien[id].Farbe, g.Sum(t => t.Brutto))
                    : new KategorieAnteilDto(null, OhneKategorieFarbe, g.Sum(t => t.Brutto)))
                .OrderByDescending(k => k.Summe)
                .ToArray();
            return new MonatsKostenDto(
                $"{jahr:D4}-{monat:D2}",
                imMonat.Sum(t => t.Brutto),
                tage.Where(t => t.Datum.Year == jahr - 1 && t.Datum.Month == monat).Sum(t => t.Brutto),
                nachKategorie);
        }).ToArray();

    private async Task<TopKorrespondentDto[]> TopKorrespondentenAsync(int jahr, CancellationToken ct)
    {
        var top = await KostenBelege()
            .Where(b => b.KorrespondentId != null && b.Rechnungsdatum!.Value.Year == jahr)
            .Select(b => new
            {
                KorrespondentId = b.KorrespondentId!.Value,
                Brutto = b.Belegart == Belegart.Gutschrift ? -Math.Abs(b.Brutto!.Value) : b.Brutto!.Value,
            })
            .GroupBy(b => b.KorrespondentId)
            .Select(g => new { Id = g.Key, Summe = g.Sum(b => b.Brutto), Anzahl = g.Count() })
            .OrderByDescending(g => g.Summe)
            .Take(AnzahlTopKorrespondenten)
            .ToListAsync(ct);

        var ids = top.Select(t => t.Id).ToList();
        var korrespondenten = await db.Korrespondenten.AsNoTracking()
            .Where(k => ids.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id, ct);
        return top
            .Where(t => korrespondenten.ContainsKey(t.Id))
            .Select(t => new TopKorrespondentDto(
                new VerweisDto(t.Id, korrespondenten[t.Id].Name),
                Logo(korrespondenten[t.Id]),
                t.Summe,
                t.Anzahl))
            .ToArray();
    }

    private async Task<ZuPruefenEintragDto[]> ZuPruefenListeAsync(CancellationToken ct)
    {
        var belege = await FertigeBelege()
            .Where(b => b.Tags.Any(t => t.TagId == SystemIds.TagZuPruefen))
            .OrderByDescending(b => b.ErstelltAm).ThenByDescending(b => b.Id)
            .Take(AnzahlZuPruefen)
            .Select(b => new
            {
                b.Id,
                Korrespondent = b.Korrespondent == null ? null : b.Korrespondent.Name,
                b.Rechnungsnummer,
                b.Brutto,
                Gruende = b.Befunde
                    .Where(f => f.Ergebnis == PruefErgebnis.Fehlgeschlagen)
                    .OrderBy(f => f.Zeitpunkt).ThenBy(f => f.Pruefung)
                    .Select(f => f.Detail)
                    .ToList(),
            })
            .ToListAsync(ct);
        return belege
            .Select(b => new ZuPruefenEintragDto(b.Id, b.Korrespondent, b.Rechnungsnummer, b.Brutto, string.Join(" · ", b.Gruende)))
            .ToArray();
    }

    /// <summary>Überfällige (älteste zuerst), danach die nächsten Fälligkeiten.</summary>
    private async Task<OffenEintragDto[]> OffeneBelegeAsync(DateOnly heute, CancellationToken ct)
    {
        var belege = await OffeneRechnungen()
            .Where(b => b.Faelligkeitsdatum != null)
            .OrderBy(b => b.Faelligkeitsdatum).ThenBy(b => b.Id)
            .Take(AnzahlOffeneBelege)
            .Select(b => new
            {
                b.Id,
                Korrespondent = b.Korrespondent == null ? null : b.Korrespondent.Name,
                b.Rechnungsnummer,
                b.Brutto,
                b.Faelligkeitsdatum,
            })
            .ToListAsync(ct);
        return belege
            .Select(b => new OffenEintragDto(b.Id, b.Korrespondent, b.Rechnungsnummer, b.Brutto, b.Faelligkeitsdatum, b.Faelligkeitsdatum < heute))
            .ToArray();
    }

    /// <summary>Offene Rechnungen, deren Skontofrist heute oder später endet; die nächsten Fristen zuerst.</summary>
    private async Task<SkontoUebersichtDto> SkontoAsync(DateOnly heute, CancellationToken ct)
    {
        var mitSkonto = OffeneRechnungen().Where(b => b.SkontoProzent != null && b.SkontoBis >= heute);
        var summe = await mitSkonto
            .GroupBy(_ => 1)
            .Select(g => new { Anzahl = g.Count(), Ersparnis = g.Sum(b => b.SkontoBetrag ?? 0) })
            .FirstOrDefaultAsync(ct);
        var eintraege = await mitSkonto
            .OrderBy(b => b.SkontoBis).ThenBy(b => b.Id)
            .Take(AnzahlSkonto)
            .Select(b => new SkontoEintragDto(
                b.Id,
                b.Korrespondent == null ? null : b.Korrespondent.Name,
                b.Rechnungsnummer,
                b.Brutto,
                b.SkontoProzent!.Value,
                b.SkontoBis!.Value,
                b.SkontoBetrag))
            .ToArrayAsync(ct);
        return new SkontoUebersichtDto(summe?.Anzahl ?? 0, summe?.Ersparnis ?? 0, eintraege);
    }

    /// <summary>Größte prozentuale Änderung des Einzelpreises zwischen erstem und letztem Kauf im Jahr.</summary>
    private async Task<PreisaenderungDto[]> PreisaenderungenAsync(int jahr, CancellationToken ct)
    {
        var kaeufe = await KostenBelege()
            .Where(b => b.Belegart != Belegart.Gutschrift && b.Rechnungsdatum!.Value.Year == jahr)
            .SelectMany(b => b.Positionen
                .Where(p => p.ArtikelId != null && p.Einzelpreis != null)
                .Select(p => new { ArtikelId = p.ArtikelId!.Value, Einzelpreis = p.Einzelpreis!.Value, Datum = b.Rechnungsdatum!.Value, b.ErstelltAm, p.Nr }))
            .ToListAsync(ct);

        var aenderungen = kaeufe
            .GroupBy(k => k.ArtikelId)
            .Where(g => g.Count() >= 2)
            .Select(g =>
            {
                var sortiert = g.OrderBy(k => k.Datum).ThenBy(k => k.ErstelltAm).ThenBy(k => k.Nr).ToList();
                return (ArtikelId: g.Key, Alt: sortiert[0].Einzelpreis, Neu: sortiert[^1].Einzelpreis);
            })
            .Where(a => a.Alt != 0)
            .Select(a => (a.ArtikelId, a.Alt, a.Neu, Aenderung: Math.Round((a.Neu - a.Alt) / a.Alt * 100, 1, MidpointRounding.AwayFromZero)))
            .OrderByDescending(a => Math.Abs(a.Aenderung)).ThenBy(a => a.ArtikelId)
            .Take(AnzahlPreisaenderungen)
            .ToList();

        var ids = aenderungen.Select(a => a.ArtikelId).ToList();
        var artikel = await db.Artikel.AsNoTracking()
            .Include(a => a.Korrespondent)
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        return aenderungen
            .Where(a => artikel.ContainsKey(a.ArtikelId))
            .Select(a =>
            {
                var art = artikel[a.ArtikelId];
                return new PreisaenderungDto(art.Id, art.Name, false, art.Korrespondent?.Name, a.Alt, a.Neu, art.Einheit, a.Aenderung);
            })
            .ToArray();
    }

    /// <summary>Summe der Steuerbeträge je UStVA-Zeitraum; <c>null</c> für Kleinunternehmer.</summary>
    private async Task<VorsteuerDto?> VorsteuerAsync(List<TagesSumme> tage, int jahr, CancellationToken ct)
    {
        var ust = await einstellungen.LesenAsync<UstEinstellungen>(ct);
        if (ust.Status == UstStatus.Kleinunternehmer)
        {
            return null;
        }

        var imJahr = tage.Where(t => t.Datum.Year == jahr).ToList();
        VorsteuerZeileDto[] zeilen = ust.Zeitraum switch
        {
            UstvaZeitraum.Monatlich => [.. Enumerable.Range(1, 12).Select(m => new VorsteuerZeileDto(
                $"{Monatsnamen[m - 1]} {jahr}", imJahr.Where(t => t.Datum.Month == m).Sum(t => t.Steuer)))],
            UstvaZeitraum.Jaehrlich => [new VorsteuerZeileDto($"{jahr}", imJahr.Sum(t => t.Steuer))],
            _ => [.. Enumerable.Range(1, 4).Select(q => new VorsteuerZeileDto(
                $"Q{q} {jahr}", imJahr.Where(t => (t.Datum.Month - 1) / 3 + 1 == q).Sum(t => t.Steuer)))],
        };
        return new VorsteuerDto(ust.Zeitraum, zeilen, zeilen.Sum(z => z.Betrag));
    }

    private async Task<DiVerbrauchDto> DiVerbrauchAsync(DateOnly heute, CancellationToken ct)
    {
        var monat = new DateOnly(heute.Year, heute.Month, 1);
        var seiten = await db.DiVerbraeuche.AsNoTracking()
            .Where(v => v.Monat == monat)
            .Select(v => v.Seiten)
            .FirstOrDefaultAsync(ct);
        var di = await einstellungen.LesenAsync<DiEinstellungen>(ct);
        var wartend = await db.ImportVorgaenge.CountAsync(v => v.Status == ImportStatus.Pausiert, ct);
        var prozent = di.Monatslimit <= 0 ? 0 : (int)Math.Round(seiten * 100m / di.Monatslimit, MidpointRounding.AwayFromZero);
        return new DiVerbrauchDto($"{monat:yyyy-MM}", seiten, di.Monatslimit, prozent, wartend > 0, wartend);
    }

    private static LogoDto Logo(Korrespondent k)
    {
        var initialen = string.Concat(
            k.Name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => char.IsLetterOrDigit(w[0]))
                .Take(2)
                .Select(w => char.ToUpperInvariant(w[0])));
        return new LogoDto(
            k.LogoPfad is null ? null : ApiRouten.Korrespondenten.Logo.Replace("{id}", k.Id.ToString(), StringComparison.Ordinal),
            initialen.Length == 0 ? "?" : initialen,
            k.LogoPfad is null ? LogoQuelle.Keine : k.LogoQuelle);
    }

    private sealed record TagesSumme(DateOnly Datum, Guid? KategorieId, decimal Brutto, decimal Steuer, int Anzahl);
}
