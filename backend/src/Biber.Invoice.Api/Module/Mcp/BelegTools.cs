using System.ComponentModel;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Infrastructure.Module.TagsKategorien;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Biber.Invoice.Api.Module.Mcp;

/// <summary>Tools <c>belege_suchen</c> und <c>beleg_abrufen</c>.</summary>
[McpServerToolType]
public sealed class BelegTools(
    IBelegAbfrageDienst belege,
    IKorrespondentDienst korrespondenten,
    KategorieVerwaltung kategorien,
    TagVerwaltung tags,
    IConfiguration konfiguration,
    TimeProvider zeit)
{
    public const int Seitengroesse = 20;

    [McpServerTool(Name = "belege_suchen", ReadOnly = true)]
    [Description("Sucht Eingangsrechnungen nach Volltext und Filtern. Liefert 20 Belege je Seite, neueste zuerst.")]
    public async Task<string> BelegeSuchenAsync(
        [Description("Suchbegriff für Inhalt, Titel und Rechnungsnummer.")] string? suche = null,
        [Description("Rechnungsdatum ab (einschließlich), ISO-Datum JJJJ-MM-TT.")] DateOnly? von = null,
        [Description("Rechnungsdatum bis (einschließlich), ISO-Datum JJJJ-MM-TT.")] DateOnly? bis = null,
        [Description("Name des Korrespondenten (Lieferanten).")] string? korrespondent = null,
        [Description("Name der Kategorie.")] string? kategorie = null,
        [Description("Name eines Tags.")] string? tag = null,
        [Description("Bruttobetrag ab in Euro.")] decimal? betrag_von = null,
        [Description("Bruttobetrag bis in Euro.")] decimal? betrag_bis = null,
        [Description("Status: alle, inbox, zu_pruefen, offen oder ueberfaellig.")] string? status = null,
        [Description("Seite, beginnt bei 1.")] int seite = 1,
        CancellationToken ct = default)
    {
        var filter = new BelegFilterDto
        {
            Suche = string.IsNullOrWhiteSpace(suche) ? null : suche,
            Von = von,
            Bis = bis,
            BetragVon = betrag_von,
            BetragBis = betrag_bis,
            Ansicht = Ansicht(status),
            Seite = Math.Max(seite, 1),
            Seitengroesse = Seitengroesse,
        };

        if (!string.IsNullOrWhiteSpace(korrespondent))
        {
            var alle = await korrespondenten.ListeAsync(null, "Alle", zeit.GetUtcNow().Year, ct);
            filter = filter with { KorrespondentIds = [McpHilfe.Aufloesen("Korrespondent", korrespondent, alle.Select(k => (k.Id, k.Name)))] };
        }

        if (!string.IsNullOrWhiteSpace(kategorie))
        {
            var alle = await kategorien.ListeAsync(null, ct);
            filter = filter with { KategorieIds = [McpHilfe.Aufloesen("Kategorie", kategorie, alle.Select(k => (k.Id, k.Name)))] };
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var alle = await tags.ListeAsync(ct);
            filter = filter with { TagIds = [McpHilfe.Aufloesen("Tag", tag, alle.Select(t => (t.Id, t.Name)))] };
        }

        var liste = await belege.SuchenAsync(filter, ct);
        return McpHilfe.Json(new
        {
            liste.Gesamt,
            liste.Seite,
            Seiten = (int)Math.Ceiling(liste.Gesamt / (double)Seitengroesse),
            liste.SummeBrutto,
            Belege = liste.Eintraege.Select(b => new
            {
                b.Id,
                b.Rechnungsnummer,
                Korrespondent = b.Korrespondent?.Name,
                Datum = b.Rechnungsdatum,
                Faelligkeit = b.Faelligkeitsdatum,
                b.Brutto,
                Kategorie = b.Kategorie?.Name,
                Tags = b.Tags.Select(t => t.Name),
                Link = McpHilfe.BelegLink(konfiguration, b.Id),
            }),
        });
    }

    [McpServerTool(Name = "beleg_abrufen", ReadOnly = true)]
    [Description("Ruft einen Beleg mit Kopfdaten, Steuerzeilen, Positionen, Tags, Prüfbefunden und Dateien ab.")]
    public async Task<string> BelegAbrufenAsync(
        [Description("ID des Belegs (GUID).")] Guid id,
        CancellationToken ct = default)
    {
        var b = await belege.AbrufenAsync(id, ct)
            ?? throw new McpException($"Beleg {id} nicht gefunden.");

        return McpHilfe.Json(new
        {
            b.Id,
            b.Rechnungsnummer,
            b.Titel,
            Korrespondent = b.Korrespondent?.Name,
            KorrespondentId = b.Korrespondent?.Id,
            Kategorie = b.Kategorie?.Name,
            b.Belegart,
            b.Status,
            b.Format,
            Datum = b.Rechnungsdatum,
            Faelligkeit = b.Faelligkeitsdatum,
            b.Ueberfaellig,
            b.Leistungsdatum,
            b.LeistungVon,
            b.LeistungBis,
            b.Netto,
            b.Steuer,
            b.Brutto,
            b.Waehrung,
            Steuerzeilen = b.Steuerzeilen.Select(s => new { s.Satz, s.Netto, s.Steuer }),
            Positionen = b.Positionen.Select(p => new
            {
                p.Nr,
                p.Text,
                p.Artikelnummer,
                Merkmale = p.Merkmale.Select(m => new { m.Name, m.Wert }),
                p.Menge,
                p.Einheit,
                p.Einzelpreis,
                p.Gesamt,
                p.Steuersatz,
                ArtikelId = p.Artikel?.Id,
                p.StammartikelId,
                p.Grundpreis,
                p.GrundpreisEinheit,
            }),
            Tags = b.Tags.Select(t => t.Name),
            Pruefbefunde = b.Befunde.Select(f => new { f.Name, f.Ergebnis, f.Detail }),
            Dateien = b.Dateien.Select(d => new { d.Id, d.Rolle, d.MimeType, d.Groesse }),
            Link = McpHilfe.BelegLink(konfiguration, b.Id),
        });
    }

    private static BelegAnsicht Ansicht(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        null or "" or "alle" => BelegAnsicht.Alle,
        "inbox" => BelegAnsicht.Inbox,
        "zu_pruefen" => BelegAnsicht.ZuPruefen,
        "offen" => BelegAnsicht.Offen,
        "ueberfaellig" => BelegAnsicht.Ueberfaellig,
        _ => throw new McpException($"Unbekannter Status „{status}“. Erlaubt: alle, inbox, zu_pruefen, offen, ueberfaellig."),
    };
}
