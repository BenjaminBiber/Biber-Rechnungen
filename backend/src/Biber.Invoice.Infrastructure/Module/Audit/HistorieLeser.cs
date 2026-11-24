using System.Globalization;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Audit;

/// <summary>
/// Liest die Historie eines Belegs und bildet die Protokolleinträge auf lesbare Texte ab (Design „Historie“).
/// Zeitpunkte in den Werten stehen in der eingestellten Zeitzone, technische Felder unter deutschem Namen.
/// </summary>
public sealed class HistorieLeser(BiberDbContext db, IEinstellungen einstellungen)
{
    private const string Leer = "leer";
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private static readonly Dictionary<string, string> Feldnamen = new()
    {
        [nameof(Beleg.KategorieId)] = "Kategorie",
        [nameof(Beleg.KategorieManuell)] = "Kategorie manuell gesetzt",
        [nameof(Beleg.KorrespondentId)] = "Korrespondent",
        [nameof(Beleg.KorrespondentErkennung)] = "Korrespondent erkannt über",
        [nameof(Beleg.Quelle)] = "Quelle",
        [nameof(Beleg.ImportVorgangId)] = "Import-Vorgang",
        [nameof(Beleg.ExtraktionsQuelle)] = "Ausgelesen aus",
        [nameof(Beleg.DiKonfidenz)] = "Konfidenz",
        [nameof(Beleg.ExtrahiertAm)] = "Ausgelesen am",
        [nameof(Beleg.Seitenanzahl)] = "Seitenanzahl",
        [nameof(Beleg.DuplikatVonId)] = "Dublette von",
        [nameof(Beleg.AufgeteiltAusBelegId)] = "Aufgeteilt aus",
        [nameof(Beleg.BestaetigtVonId)] = "Bestätigt von",
        [nameof(Beleg.Titel)] = "Titel",
        [nameof(Beleg.Rechnungsnummer)] = "Rechnungsnummer",
        [nameof(Beleg.Rechnungsdatum)] = "Rechnungsdatum",
        [nameof(Beleg.Leistungsdatum)] = "Leistungsdatum",
        [nameof(Beleg.LeistungVon)] = "Leistungszeitraum von",
        [nameof(Beleg.LeistungBis)] = "Leistungszeitraum bis",
        [nameof(Beleg.Faelligkeitsdatum)] = "Fälligkeitsdatum",
        [nameof(Beleg.Waehrung)] = "Währung",
        [nameof(Beleg.Netto)] = "Netto",
        [nameof(Beleg.Steuer)] = "Steuer",
        [nameof(Beleg.Brutto)] = "Brutto",
        [nameof(Beleg.Status)] = "Status",
        [nameof(Beleg.Belegart)] = "Belegart",
        [nameof(Beleg.Richtung)] = "Richtung",
        [nameof(Beleg.BestaetigtAm)] = "Bestätigung",
        [nameof(Position.Text)] = "Text",
        [nameof(Position.Menge)] = "Menge",
        [nameof(Position.Einzelpreis)] = "Einzelpreis",
        [nameof(Position.Gesamt)] = "Gesamt",
        [nameof(Position.Steuersatz)] = "Steuersatz",
        [nameof(Steuerzeile.Satz)] = "Steuersatz",
    };

    /// <summary>Historie, neueste zuerst; <c>null</c>, wenn es den Beleg nicht gibt.</summary>
    public async Task<IReadOnlyList<AuditEintragDto>?> LesenAsync(Guid belegId, CancellationToken ct)
    {
        var beleg = await db.Belege.AsNoTracking().FirstOrDefaultAsync(b => b.Id == belegId, ct);
        if (beleg is null)
        {
            return null;
        }

        var eintraege = await db.AuditEintraege.AsNoTracking()
            .Where(a => a.BelegId == belegId)
            .OrderByDescending(a => a.Zeitpunkt).ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        var namen = await NamenAsync(eintraege, ct);
        var zone = Zeitzone((await einstellungen.LesenAsync<AllgemeinEinstellungen>(ct)).Zeitzone);
        var dateiname = beleg.ImportVorgangId is { } vorgangId
            ? await db.ImportVorgaenge.Where(v => v.Id == vorgangId).Select(v => v.Dateiname).FirstOrDefaultAsync(ct)
            : null;

        var ergebnis = new List<AuditEintragDto>();
        var verarbeitet = new HashSet<long>();
        foreach (var eintrag in eintraege)
        {
            if (!verarbeitet.Add(eintrag.Id))
            {
                continue;
            }

            if (eintrag.Entitaet == nameof(BelegTag))
            {
                var gruppe = eintraege
                    .Where(a => a.Entitaet == nameof(BelegTag) && a.Aktion == eintrag.Aktion
                        && a.Zeitpunkt == eintrag.Zeitpunkt && a.BenutzerId == eintrag.BenutzerId)
                    .OrderBy(a => a.Id)
                    .ToList();
                gruppe.ForEach(a => verarbeitet.Add(a.Id));
                ergebnis.Add(Tags(eintrag, gruppe));
                continue;
            }

            ergebnis.Add(Abbilden(eintrag, beleg, new Werte(namen, zone), dateiname));
        }

        return ergebnis;
    }

    /// <summary>Was zum Anzeigen der Werte nötig ist: Namen zu IDs und die Zeitzone für Zeitpunkte.</summary>
    private sealed record Werte(Dictionary<string, string> Namen, TimeZoneInfo Zone);

    private static TimeZoneInfo Zeitzone(string id) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : TimeZoneInfo.Utc;

    private async Task<Dictionary<string, string>> NamenAsync(List<AuditEintrag> eintraege, CancellationToken ct)
    {
        var ids = eintraege
            .Where(a => a.Feld is nameof(Beleg.KategorieId) or nameof(Beleg.KorrespondentId))
            .SelectMany(a => new[] { a.AlterWert, a.NeuerWert })
            .Select(w => Guid.TryParse(w, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var kategorien = await db.Kategorien.Where(k => ids.Contains(k.Id)).Select(k => new { k.Id, k.Name })
            .ToListAsync(ct);
        var korrespondenten = await db.Korrespondenten.Where(k => ids.Contains(k.Id)).Select(k => new { k.Id, k.Name })
            .ToListAsync(ct);
        var namen = kategorien.Concat(korrespondenten).ToDictionary(k => k.Id.ToString(), k => k.Name);

        // „Bestätigt von“ nennt eine Benutzer-ID; ihr Name steht an den Einträgen, die dieser Benutzer geschrieben hat.
        foreach (var a in eintraege.Where(a => a.BenutzerId is not null))
        {
            namen.TryAdd(a.BenutzerId!.Value.ToString(), a.BenutzerName);
        }

        return namen;
    }

    private static AuditEintragDto Tags(AuditEintrag erster, List<AuditEintrag> gruppe)
    {
        var gesetzt = erster.Aktion == AuditInterceptor.Angelegt;
        var tagNamen = gruppe.Select(a => (gesetzt ? a.NeuerWert : a.AlterWert) ?? a.EntitaetId).ToList();
        var text = (gesetzt, tagNamen.Count) switch
        {
            (true, 1) => "Tag gesetzt: ",
            (true, _) => "Tags gesetzt: ",
            (false, 1) => "Tag entfernt: ",
            (false, _) => "Tags entfernt: ",
        } + string.Join(", ", tagNamen);
        var warnung = gesetzt && gruppe.Any(a => a.EntitaetId == SystemIds.TagZuPruefen.ToString());
        return Dto(erster, text, null, null, null, warnung ? "warnung" : null);
    }

    private static AuditEintragDto Abbilden(AuditEintrag a, Beleg beleg, Werte werte, string? dateiname)
    {
        var system = a.BenutzerId is null;
        switch (a.Entitaet, a.Aktion)
        {
            case (nameof(Beleg), AuditInterceptor.Angelegt):
                return Dto(a, string.IsNullOrEmpty(dateiname)
                    ? $"Importiert aus {QuelleText(beleg.Quelle)}"
                    : $"Importiert aus {QuelleText(beleg.Quelle)}: {dateiname}");
            case (nameof(Beleg), AuditInterceptor.Geloescht):
                return Dto(a, "Beleg gelöscht");
            case (nameof(Beleg), AuditInterceptor.Geaendert):
                return Feldaenderung(a, beleg, werte, system);
            case (nameof(BelegDatei), AuditInterceptor.Angelegt) when a.Feld == nameof(DateiRolle.Original):
                return Dto(a, $"Abgelegt unter {AblagePfad(a.NeuerWert)}");
            case (nameof(BelegDatei), AuditInterceptor.Angelegt):
                return Dto(a, $"Datei abgelegt ({a.Feld}): {a.NeuerWert}");
            case (nameof(BelegDatei), AuditInterceptor.Geloescht):
                return Dto(a, $"Datei entfernt ({a.Feld}): {a.AlterWert}");
            case (nameof(PruefBefund), AuditInterceptor.Angelegt):
                return Dto(a, a.NeuerWert == "1" ? "1 Prüfbefund erstellt" : $"{a.NeuerWert} Prüfbefunde erstellt");
            case (nameof(PruefBefund), AuditInterceptor.Geloescht):
                return Dto(a, a.AlterWert == "1" ? "1 Prüfbefund entfernt" : $"{a.AlterWert} Prüfbefunde entfernt");
            case (nameof(Position), AuditInterceptor.Angelegt):
                return Dto(a, $"Position hinzugefügt: {a.NeuerWert}");
            case (nameof(Position), AuditInterceptor.Geloescht):
                return Dto(a, $"Position entfernt: {a.AlterWert}");
            case (nameof(Steuerzeile), AuditInterceptor.Angelegt):
                return Dto(a, $"Steuerzeile hinzugefügt: {Zahl(a.NeuerWert)} %");
            case (nameof(Steuerzeile), AuditInterceptor.Geloescht):
                return Dto(a, $"Steuerzeile entfernt: {Zahl(a.AlterWert)} %");
            case (nameof(Position) or nameof(Steuerzeile), AuditInterceptor.Geaendert):
                return FeldDto(a, $"{a.Entitaet} – {Feldname(a.Feld)}", werte);
            default:
                // Notizen über IAuditSchreiber: ihr Text, ersatzweise die Aktion.
                return Dto(a, a.NeuerWert ?? a.Aktion);
        }
    }

    private static AuditEintragDto Feldaenderung(AuditEintrag a, Beleg beleg, Werte werte, bool system)
    {
        var namen = werte.Namen;
        if (system && a.Feld == nameof(Beleg.ExtrahiertAm) && a.NeuerWert is not null)
        {
            var seiten = beleg.Seitenanzahl == 1 ? "1 Seite" : $"{beleg.Seitenanzahl} Seiten";
            var konfidenz = beleg.DiKonfidenz is { } k ? $", Konfidenz {k.ToString("0.00", Deutsch)}" : "";
            return Dto(a, $"Ausgelesen mit {ExtraktionText(beleg.ExtraktionsQuelle)}, {seiten}{konfidenz}");
        }

        if (system && a.Feld == nameof(Beleg.KorrespondentId) && a.NeuerWert is not null)
        {
            return Dto(a,
                $"Korrespondent zugeordnet: {Name(a.NeuerWert, namen)} (über {ErkennungText(beleg.KorrespondentErkennung)})");
        }

        if (system && a.Feld == nameof(Beleg.KategorieId) && a.NeuerWert is not null)
        {
            return Dto(a, $"Kategorie „{Name(a.NeuerWert, namen)}“ gesetzt");
        }

        return FeldDto(a, Feldname(a.Feld), werte);
    }

    private static AuditEintragDto FeldDto(AuditEintrag a, string feldname, Werte werte)
    {
        var text = a.AlterWert is null ? $"{feldname} ergänzt" : $"{feldname} geändert";
        return Dto(a, text, a.Feld, Anzeigewert(a.Feld, a.AlterWert, werte), Anzeigewert(a.Feld, a.NeuerWert, werte));
    }

    private static AuditEintragDto Dto(AuditEintrag a, string text, string? feld = null, string? alt = null,
        string? neu = null, string? art = null) =>
        new(
            IdAus(a.Id),
            a.Zeitpunkt,
            a.BenutzerName,
            a.BenutzerId is null,
            a.Aktion,
            text,
            feld,
            alt,
            neu,
            art ?? (a.BenutzerId is null ? "system" : "benutzer"));

    /// <summary>Die Protokoll-ID ist eine Zahl; der Vertrag erwartet eine Guid, die sie verlustfrei trägt.</summary>
    private static Guid IdAus(long id) => new(0, 0, 0, BitConverter.GetBytes(id));

    private static string Feldname(string? feld) =>
        feld is not null && Feldnamen.TryGetValue(feld, out var name) ? name : feld ?? "";

    private static string Anzeigewert(string? feld, string? wert, Werte werte)
    {
        if (string.IsNullOrEmpty(wert))
        {
            return Leer;
        }

        if (feld is nameof(Beleg.KategorieId) or nameof(Beleg.KorrespondentId) or nameof(Beleg.BestaetigtVonId))
        {
            return Name(wert, werte.Namen);
        }

        if (bool.TryParse(wert, out var ja))
        {
            return ja ? "ja" : "nein";
        }

        if (DateOnly.TryParseExact(wert, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var datum))
        {
            return datum.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
        }

        if (DateTimeOffset.TryParseExact(wert, "O", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var zeitpunkt))
        {
            return TimeZoneInfo.ConvertTime(zeitpunkt, werte.Zone).ToString("dd.MM.yyyy, HH:mm", CultureInfo.InvariantCulture);
        }

        return Zahl(wert);
    }

    private static string Zahl(string? wert) =>
        decimal.TryParse(wert, NumberStyles.Number, CultureInfo.InvariantCulture, out var zahl)
            ? zahl.ToString(Deutsch)
            : wert ?? Leer;

    private static string Name(string id, Dictionary<string, string> namen) =>
        namen.TryGetValue(id, out var name) ? name : id;

    private static string AblagePfad(string? pfad) =>
        pfad is null ? "" : pfad.StartsWith("ablage/", StringComparison.Ordinal) ? pfad : $"ablage/{pfad.TrimStart('/')}";

    private static string QuelleText(ImportQuelle quelle) => quelle switch
    {
        ImportQuelle.Ordner => "Eingangsordner",
        ImportQuelle.Mail => "E-Mail",
        ImportQuelle.Mcp => "MCP",
        _ => "Upload",
    };

    private static string ExtraktionText(ExtraktionsQuelle quelle) => quelle switch
    {
        ExtraktionsQuelle.DocumentIntelligence => "Document Intelligence",
        _ => quelle.ToString(),
    };

    private static string ErkennungText(KorrespondentErkennung erkennung) => erkennung switch
    {
        KorrespondentErkennung.UstIdNr => "USt-IdNr.",
        KorrespondentErkennung.Iban => "IBAN",
        KorrespondentErkennung.Name => "Name",
        KorrespondentErkennung.Manuell => "manuelle Zuordnung",
        KorrespondentErkennung.Auto => "Auto-Matching",
        _ => "keine Erkennung",
    };
}
