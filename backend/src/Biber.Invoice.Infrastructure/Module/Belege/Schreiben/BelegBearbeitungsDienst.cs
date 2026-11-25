using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Korrespondenten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Belege.Schreiben;

/// <summary>
/// Manuelle Korrektur eines Belegs: Kopf, Steuerzeilen, Positionen, Tags, Bestätigen, Dublette, erneutes Auslesen
/// und Massenaktionen. Der Inhalt der Originaldatei in der Ablage wird nie angefasst (A3); nur solange der Beleg
/// „zu prüfen“ ist, zieht sie beim Wechsel des Korrespondenten in dessen Ordner um. „zu prüfen“ setzt nur die Pipeline
/// und entfernt nur das Bestätigen (B3); nach einer Korrektur werden lediglich die Befunde neu berechnet.
/// </summary>
public sealed class BelegBearbeitungsDienst(
    BiberDbContext db,
    IAblageSchreiber ablage,
    IBelegAbfrageDienst abfrage,
    IPruefDienst pruefDienst,
    IVerarbeitungsStarter starter,
    IEnumerable<IPipelineSchritt> schritte,
    IAktuellerBenutzer benutzer,
    TimeProvider zeit,
    ILogger<BelegBearbeitungsDienst> log) : IBelegBearbeitungsDienst
{
    public const string AuditAktionBestaetigt = "Bestaetigt";
    public const int MaxMassenaktion = 500;
    private const int MaxTitel = 500;
    private const int MaxRechnungsnummer = 200;
    private const int MaxEinheit = 50;
    private const int MaxBegruendung = 2000;

    public async Task<BelegDetailDto?> AendernAsync(Guid id, BelegAendernDto aenderung, CancellationToken ct)
    {
        var beleg = await db.Belege
            .Include(b => b.Steuerzeilen)
            .Include(b => b.Positionen)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return null;
        }

        PruefeAenderbar(beleg);
        var korrespondent = await PruefeAenderungAsync(aenderung, ct);

        var korrespondentGewechselt = aenderung.KorrespondentId != beleg.KorrespondentId;
        var zuPruefen = await db.BelegTags.AnyAsync(t => t.BelegId == id && t.TagId == SystemIds.TagZuPruefen, ct);
        if (korrespondentGewechselt)
        {
            beleg.KorrespondentId = aenderung.KorrespondentId;
            beleg.Korrespondent = korrespondent;
            beleg.KorrespondentErkennung = korrespondent is null ? KorrespondentErkennung.Keine : KorrespondentErkennung.Manuell;
            if (korrespondent is not null)
            {
                korrespondent.Geprueft = true;
            }
        }

        if (aenderung.KategorieId is null)
        {
            beleg.KategorieManuell = false;
            beleg.KategorieId = korrespondent?.DefaultKategorieId;
        }
        else if (aenderung.KategorieId != beleg.KategorieId)
        {
            beleg.KategorieId = aenderung.KategorieId;
            beleg.KategorieManuell = true;
        }

        beleg.Belegart = aenderung.Belegart;
        beleg.Rechnungsnummer = Leer(aenderung.Rechnungsnummer);
        beleg.Titel = Leer(aenderung.Titel);
        beleg.Rechnungsdatum = aenderung.Rechnungsdatum;
        beleg.Leistungsdatum = aenderung.Leistungsdatum;
        beleg.LeistungVon = aenderung.LeistungVon;
        beleg.LeistungBis = aenderung.LeistungBis;
        beleg.Faelligkeitsdatum = aenderung.Faelligkeitsdatum;
        beleg.Waehrung = aenderung.Waehrung.Trim().ToUpperInvariant();
        beleg.Netto = aenderung.Netto;
        beleg.Steuer = aenderung.Steuer;
        beleg.Brutto = aenderung.Brutto;

        db.Steuerzeilen.RemoveRange(beleg.Steuerzeilen);
        beleg.Steuerzeilen = [.. (aenderung.Steuerzeilen ?? []).Select(z => new Steuerzeile
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Satz = z.Satz,
            Netto = z.Netto,
            Steuer = z.Steuer,
        })];
        db.Steuerzeilen.AddRange(beleg.Steuerzeilen);

        if (korrespondentGewechselt)
        {
            await ArtikelZuordnenAsync(beleg, ct);
        }

        await pruefDienst.PruefenAsync(beleg, null, ct);

        // Nur ungeprüfte Belege ziehen um; bestätigte bleiben dort, wo sie abgelegt wurden.
        var verschoben = korrespondentGewechselt && zuPruefen ? await ablage.VerschiebenAsync(beleg, ct) : [];
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            ablage.Zuruecknehmen(verschoben);
            throw;
        }

        foreach (var v in verschoben)
        {
            log.LogInformation("Ablage von Beleg {Beleg} verschoben: {Von} → {Nach}", id, v.Von, v.Nach);
        }

        log.LogInformation("Beleg {Beleg} manuell korrigiert", id);
        return await abfrage.AbrufenAsync(id, ct);
    }

    public async Task<BelegDetailDto?> PositionenErsetzenAsync(Guid id, IReadOnlyList<PositionAendernDto> positionen, CancellationToken ct)
    {
        var beleg = await db.Belege.Include(b => b.Positionen).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return null;
        }

        PruefeAenderbar(beleg);
        PruefePositionen(positionen ?? []);

        db.Positionen.RemoveRange(beleg.Positionen);
        beleg.Positionen = [.. (positionen ?? []).Select((p, i) => new Position
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Nr = i + 1,
            Text = p.Text.Trim(),
            Menge = p.Menge,
            Einheit = Leer(p.Einheit),
            Einzelpreis = p.Einzelpreis,
            Gesamt = p.Gesamt,
            Steuersatz = p.Steuersatz,
        })];
        db.Positionen.AddRange(beleg.Positionen);

        await ArtikelZuordnenAsync(beleg, ct);
        await pruefDienst.PruefenAsync(beleg, null, ct);
        await db.SaveChangesAsync(ct);
        return await abfrage.AbrufenAsync(id, ct);
    }

    public async Task<bool> TagHinzufuegenAsync(Guid id, Guid tagId, CancellationToken ct)
    {
        var beleg = await db.Belege.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return false;
        }

        PruefeAenderbar(beleg);
        PruefeKeinZuPruefen(tagId);
        if (!await db.Tags.AnyAsync(t => t.Id == tagId, ct))
        {
            throw BelegValidierungsAusnahme.Fuer("tagId", "Den Tag gibt es nicht.");
        }

        if (!await db.BelegTags.AnyAsync(t => t.BelegId == id && t.TagId == tagId, ct))
        {
            db.BelegTags.Add(NeuerTag(id, tagId));
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<bool> TagEntfernenAsync(Guid id, Guid tagId, CancellationToken ct)
    {
        var beleg = await db.Belege.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return false;
        }

        PruefeAenderbar(beleg);
        PruefeKeinZuPruefen(tagId);
        if (await db.BelegTags.FirstOrDefaultAsync(t => t.BelegId == id && t.TagId == tagId, ct) is { } zuordnung)
        {
            db.BelegTags.Remove(zuordnung);
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<bool> BestaetigenAsync(Guid id, string? begruendung, CancellationToken ct)
    {
        var beleg = await db.Belege.Include(b => b.Tags).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return false;
        }

        PruefeAenderbar(beleg);
        begruendung = Leer(begruendung);
        if (begruendung?.Length > MaxBegruendung)
        {
            throw BelegValidierungsAusnahme.Fuer("begruendung", $"Die Begründung darf höchstens {MaxBegruendung} Zeichen lang sein.");
        }

        if (await BestaetigeAsync(beleg, begruendung, ct))
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Beleg {Beleg} bestätigt", id);
        }

        return true;
    }

    public async Task<AnzahlGeaendertDto> MassenaktionAsync(MassenaktionDto aktion, CancellationToken ct)
    {
        var ids = (aktion.BelegIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            throw BelegValidierungsAusnahme.Fuer("belegIds", "Es ist kein Beleg ausgewählt.");
        }

        if (ids.Count > MaxMassenaktion)
        {
            throw BelegValidierungsAusnahme.Fuer("belegIds", $"Höchstens {MaxMassenaktion} Belege je Massenaktion.");
        }

        var tagIds = (aktion.TagIds ?? []).Distinct().ToList();
        await PruefeMassenaktionAsync(aktion, tagIds, ct);

        var belege = await db.Belege.Include(b => b.Tags).Where(b => ids.Contains(b.Id)).ToListAsync(ct);
        if (belege.Count != ids.Count)
        {
            throw BelegValidierungsAusnahme.Fuer("belegIds", $"{ids.Count - belege.Count} der Belege gibt es nicht.");
        }

        if (belege.Any(b => b.Status == BelegStatus.InVerarbeitung))
        {
            throw new BelegKonfliktAusnahme("Mindestens ein Beleg wird gerade verarbeitet.");
        }

        var geaendert = 0;
        foreach (var beleg in belege)
        {
            if (await AnwendenAsync(beleg, aktion, tagIds, ct))
            {
                geaendert++;
            }
        }

        // Ein SaveChanges ist bereits atomar; die Transaktion umschließt auch die Schreibvorgänge des Audit-Interceptors.
        await using var transaktion = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await transaktion.CommitAsync(ct);
        log.LogInformation("Massenaktion {Aktion} auf {Anzahl} Belege, {Geaendert} geändert", aktion.Aktion, ids.Count, geaendert);
        return new AnzahlGeaendertDto(geaendert);
    }

    public async Task<bool> DubletteSetzenAsync(Guid id, Guid? duplikatVonId, CancellationToken ct)
    {
        var beleg = await db.Belege.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return false;
        }

        PruefeAenderbar(beleg);
        if (duplikatVonId is { } vonId)
        {
            if (vonId == id)
            {
                throw BelegValidierungsAusnahme.Fuer("duplikatVonId", "Ein Beleg kann nicht Dublette von sich selbst sein.");
            }

            var original = await db.Belege.AsNoTracking()
                .Where(b => b.Id == vonId)
                .Select(b => new { b.DuplikatVonId })
                .FirstOrDefaultAsync(ct)
                ?? throw BelegValidierungsAusnahme.Fuer("duplikatVonId", "Den Beleg gibt es nicht.");
            if (original.DuplikatVonId is not null)
            {
                throw BelegValidierungsAusnahme.Fuer("duplikatVonId", "Der Beleg ist selbst eine Dublette.");
            }
        }

        if (beleg.DuplikatVonId != duplikatVonId)
        {
            beleg.DuplikatVonId = duplikatVonId;
            await pruefDienst.PruefenAsync(beleg, null, ct);
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<bool> ErneutAuslesenAsync(Guid id, CancellationToken ct)
    {
        var beleg = await db.Belege.AsNoTracking().Select(b => new { b.Id, b.Status }).FirstOrDefaultAsync(b => b.Id == id, ct);
        if (beleg is null)
        {
            return false;
        }

        if (beleg.Status == BelegStatus.InVerarbeitung)
        {
            throw new BelegKonfliktAusnahme("Der Beleg wird gerade verarbeitet.");
        }

        // Manuell gesetzte Kategorie und Korrespondent schützen die Pipeline-Schritte selbst (KategorieManuell, Erkennung Manuell).
        await starter.ErneutVerarbeitenAsync(id, ct);
        log.LogInformation("Beleg {Beleg} erneut ausgelesen", id);
        return true;
    }

    private async Task<bool> AnwendenAsync(Beleg beleg, MassenaktionDto aktion, List<Guid> tagIds, CancellationToken ct)
    {
        switch (aktion.Aktion)
        {
            case MassenaktionArt.TagsSetzen:
                var fehlend = tagIds.Where(t => beleg.Tags.All(bt => bt.TagId != t)).ToList();
                db.BelegTags.AddRange(fehlend.Select(t => NeuerTag(beleg.Id, t)));
                return fehlend.Count > 0;
            case MassenaktionArt.TagsEntfernen:
                var vorhanden = beleg.Tags.Where(bt => tagIds.Contains(bt.TagId)).ToList();
                db.BelegTags.RemoveRange(vorhanden);
                return vorhanden.Count > 0;
            case MassenaktionArt.KategorieSetzen:
                if (beleg.KategorieId == aktion.KategorieId && beleg.KategorieManuell)
                {
                    return false;
                }

                beleg.KategorieId = aktion.KategorieId;
                beleg.KategorieManuell = true;
                return true;
            case MassenaktionArt.Bestaetigen:
                return await BestaetigeAsync(beleg, null, ct);
            default:
                throw BelegValidierungsAusnahme.Fuer("aktion", "Unbekannte Aktion.");
        }
    }

    /// <summary>Entfernt „zu prüfen“ und protokolliert das Bestätigen; ohne den Tag ändert sich nichts.</summary>
    /// <remarks><see cref="Beleg.Tags"/> muss geladen sein.</remarks>
    private async Task<bool> BestaetigeAsync(Beleg beleg, string? begruendung, CancellationToken ct)
    {
        var zuPruefen = beleg.Tags.FirstOrDefault(t => t.TagId == SystemIds.TagZuPruefen);
        if (zuPruefen is null)
        {
            return false;
        }

        var jetzt = zeit.GetUtcNow();
        db.BelegTags.Remove(zuPruefen);
        beleg.BestaetigtAm = jetzt;
        beleg.BestaetigtVonId = benutzer.Id;
        if (beleg.KorrespondentId is { } korrespondentId
            && await db.Korrespondenten.FirstOrDefaultAsync(k => k.Id == korrespondentId, ct) is { } korrespondent)
        {
            korrespondent.Geprueft = true;
            if (beleg.Richtung == Richtung.Eingang)
            {
                // Erst das Bestätigen macht eine abweichende IBAN des Belegs beim Korrespondenten bekannt.
                BankverbindungenAbgleich.Ergaenzen(
                    korrespondent,
                    beleg.Bankverbindungen.Where(b => KorrespondentNormalisierung.IstIban(b.Iban)),
                    neueUebernehmen: true);
            }
        }

        db.AuditEintraege.Add(new AuditEintrag
        {
            Zeitpunkt = jetzt,
            BenutzerId = benutzer.Id,
            BenutzerName = benutzer.IstSystem || benutzer.Id is null ? "System" : benutzer.Anzeigename,
            Entitaet = nameof(Beleg),
            EntitaetId = beleg.Id.ToString(),
            BelegId = beleg.Id,
            Aktion = AuditAktionBestaetigt,
            NeuerWert = begruendung,
        });
        return true;
    }

    /// <summary>Ordnet die Positionen über den Pipeline-Schritt „Positionen“ den Artikeln des Korrespondenten zu.</summary>
    private async Task ArtikelZuordnenAsync(Beleg beleg, CancellationToken ct)
    {
        var schritt = schritte.FirstOrDefault(s => s.Reihenfolge == SchrittReihenfolge.Positionen);
        if (schritt is null)
        {
            return;
        }

        var kontext = new VerarbeitungsKontext
        {
            Vorgang = new ImportVorgang { Id = Guid.NewGuid(), Quelle = beleg.Quelle, BelegId = beleg.Id },
            Beleg = beleg,
            DateiPfad = "",
            Dateiname = "",
            IstWiederholung = true,
        };
        await schritt.AusfuehrenAsync(kontext, ct);
    }

    private async Task<Korrespondent?> PruefeAenderungAsync(BelegAendernDto a, CancellationToken ct)
    {
        Dictionary<string, string[]> fehler = [];
        void Fehler(string feld, string meldung) => fehler[feld] = [meldung];

        if (a.Waehrung is null || a.Waehrung.Trim().Length != 3 || !a.Waehrung.Trim().All(char.IsAsciiLetter))
        {
            Fehler("waehrung", "Die Währung besteht aus drei Buchstaben (ISO 4217).");
        }

        if (a.Titel?.Trim().Length > MaxTitel)
        {
            Fehler("titel", $"Der Titel darf höchstens {MaxTitel} Zeichen lang sein.");
        }

        if (a.Rechnungsnummer?.Trim().Length > MaxRechnungsnummer)
        {
            Fehler("rechnungsnummer", $"Die Rechnungsnummer darf höchstens {MaxRechnungsnummer} Zeichen lang sein.");
        }

        if (!Enum.IsDefined(a.Belegart))
        {
            Fehler("belegart", "Unbekannte Belegart.");
        }

        foreach (var (feld, datum) in new[]
        {
            ("rechnungsdatum", a.Rechnungsdatum), ("leistungsdatum", a.Leistungsdatum), ("leistungVon", a.LeistungVon),
            ("leistungBis", a.LeistungBis), ("faelligkeitsdatum", a.Faelligkeitsdatum),
        })
        {
            if (datum is { Year: < 2000 or > 2100 })
            {
                Fehler(feld, "Das Datum muss zwischen 2000 und 2100 liegen.");
            }
        }

        if (a.LeistungVon is { } von && a.LeistungBis is { } bis && von > bis)
        {
            Fehler("leistungBis", "Das Ende des Leistungszeitraums liegt vor dessen Beginn.");
        }

        if (a.Belegart != Belegart.Gutschrift)
        {
            foreach (var (feld, betrag) in new[] { ("netto", a.Netto), ("steuer", a.Steuer), ("brutto", a.Brutto) })
            {
                if (betrag < 0)
                {
                    Fehler(feld, "Der Betrag darf nicht negativ sein.");
                }
            }
        }

        var zeilen = a.Steuerzeilen ?? [];
        for (var i = 0; i < zeilen.Length; i++)
        {
            var zeile = zeilen[i];
            if (zeile is null)
            {
                Fehler($"steuerzeilen[{i}]", "Die Steuerzeile fehlt.");
                continue;
            }

            if (zeile.Satz is < 0 or > 100)
            {
                Fehler($"steuerzeilen[{i}].satz", "Der Steuersatz liegt zwischen 0 und 100 %.");
            }

            if (a.Belegart != Belegart.Gutschrift && (zeile.Netto < 0 || zeile.Steuer < 0))
            {
                Fehler($"steuerzeilen[{i}]", "Die Beträge dürfen nicht negativ sein.");
            }
        }

        if (zeilen.Where(z => z is not null).GroupBy(z => z.Satz).Any(g => g.Count() > 1))
        {
            Fehler("steuerzeilen", "Jeder Steuersatz darf nur einmal vorkommen.");
        }

        Korrespondent? korrespondent = null;
        if (a.KorrespondentId is { } korrespondentId)
        {
            korrespondent = await db.Korrespondenten.FirstOrDefaultAsync(k => k.Id == korrespondentId, ct);
            if (korrespondent is null)
            {
                Fehler("korrespondentId", "Den Korrespondenten gibt es nicht.");
            }
        }

        if (a.KategorieId is { } kategorieId && !await db.Kategorien.AnyAsync(k => k.Id == kategorieId, ct))
        {
            Fehler("kategorieId", "Die Kategorie gibt es nicht.");
        }

        if (fehler.Count > 0)
        {
            throw new BelegValidierungsAusnahme(fehler);
        }

        return korrespondent;
    }

    private static void PruefePositionen(IReadOnlyList<PositionAendernDto> positionen)
    {
        Dictionary<string, string[]> fehler = [];
        for (var i = 0; i < positionen.Count; i++)
        {
            var p = positionen[i];
            if (p is null)
            {
                fehler[$"[{i}]"] = ["Die Position fehlt."];
                continue;
            }

            if (string.IsNullOrWhiteSpace(p.Text))
            {
                fehler[$"[{i}].text"] = ["Der Text der Position fehlt."];
            }

            if (p.Einheit?.Trim().Length > MaxEinheit)
            {
                fehler[$"[{i}].einheit"] = [$"Die Einheit darf höchstens {MaxEinheit} Zeichen lang sein."];
            }

            if (p.Steuersatz is < 0 or > 100)
            {
                fehler[$"[{i}].steuersatz"] = ["Der Steuersatz liegt zwischen 0 und 100 %."];
            }
        }

        if (fehler.Count > 0)
        {
            throw new BelegValidierungsAusnahme(fehler);
        }
    }

    private async Task PruefeMassenaktionAsync(MassenaktionDto aktion, List<Guid> tagIds, CancellationToken ct)
    {
        switch (aktion.Aktion)
        {
            case MassenaktionArt.TagsSetzen or MassenaktionArt.TagsEntfernen:
                if (tagIds.Count == 0)
                {
                    throw BelegValidierungsAusnahme.Fuer("tagIds", "Es ist kein Tag angegeben.");
                }

                if (tagIds.Contains(SystemIds.TagZuPruefen))
                {
                    throw BelegValidierungsAusnahme.Fuer("tagIds", "„zu prüfen“ setzt die Prüfung; entfernt wird der Tag nur durch Bestätigen.");
                }

                if (await db.Tags.CountAsync(t => tagIds.Contains(t.Id), ct) != tagIds.Count)
                {
                    throw BelegValidierungsAusnahme.Fuer("tagIds", "Mindestens einen der Tags gibt es nicht.");
                }

                break;
            case MassenaktionArt.KategorieSetzen:
                if (aktion.KategorieId is not { } kategorieId || !await db.Kategorien.AnyAsync(k => k.Id == kategorieId, ct))
                {
                    throw BelegValidierungsAusnahme.Fuer("kategorieId", "Die Kategorie fehlt oder es gibt sie nicht.");
                }

                break;
            case MassenaktionArt.Bestaetigen:
                break;
            default:
                throw BelegValidierungsAusnahme.Fuer("aktion", "Unbekannte Aktion.");
        }
    }

    private static void PruefeAenderbar(Beleg beleg)
    {
        if (beleg.Status == BelegStatus.InVerarbeitung)
        {
            throw new BelegKonfliktAusnahme("Der Beleg wird gerade verarbeitet und kann nicht geändert werden.");
        }
    }

    private static void PruefeKeinZuPruefen(Guid tagId)
    {
        if (tagId == SystemIds.TagZuPruefen)
        {
            throw new BelegKonfliktAusnahme("„zu prüfen“ setzt die Prüfung; entfernt wird der Tag nur durch Bestätigen.");
        }
    }

    private BelegTag NeuerTag(Guid belegId, Guid tagId) =>
        new() { BelegId = belegId, TagId = tagId, GesetztAm = zeit.GetUtcNow(), GesetztVonId = benutzer.Id };

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
