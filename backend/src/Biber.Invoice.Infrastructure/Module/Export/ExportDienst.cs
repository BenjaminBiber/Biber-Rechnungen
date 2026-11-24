using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Export;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Export;

/// <summary>
/// Ergebnis von <see cref="ExportDienst.ErzeugenAsync"/>: der Lauf, die Ablagepfade mit abweichendem Hash oder
/// <see cref="NichtsZuExportieren"/>, wenn nach Wegfall der Vorsteuer (Kleinunternehmer) kein Bestandteil bleibt.
/// </summary>
public sealed record ExportErgebnis(ExportLaufDto? Lauf, IReadOnlyList<string> Abweichungen, bool NichtsZuExportieren = false);

/// <summary>Datei eines Exportlaufs; <see cref="Pfad"/> ist <c>null</c>, wenn sie auf der Platte fehlt.</summary>
public sealed record ExportDatei(string? Pfad, string Dateiname);

/// <summary>
/// Vorschau und Erzeugung von Exporten (Belegliste, Summen je Kategorie, Vorsteuer, ZIP der Originale) sowie
/// die Liste der Exportläufe. Dateien liegen unter <c>Pfade:Daten/exporte/</c>.
/// </summary>
public sealed partial class ExportDienst(
    BiberDbContext db,
    IEinstellungen einstellungen,
    IAuditSchreiber audit,
    IAktuellerBenutzer benutzer,
    IConfiguration konfiguration,
    TimeProvider zeit,
    ILogger<ExportDienst> logger)
{
    public const string AuditEntitaet = "Export";
    public const string AuditAktion = "Erstellt";
    public const string Xlsx = "xlsx";
    public const string Csv = "csv";
    public const string Pruefsummen = "pruefsummen.sha256";
    public const string Originale = "originale/";
    public const int AnzahlLaeufe = 20;

    /// <summary>Prüft die Anfrage; <c>null</c>, wenn sie gültig ist.</summary>
    public static Dictionary<string, string[]>? Pruefen(ExportAnfrageDto anfrage, bool bestandteilNoetig)
    {
        var fehler = new Dictionary<string, string[]>();
        if (anfrage.Von > anfrage.Bis)
        {
            fehler["bis"] = ["„Bis“ darf nicht vor „Von“ liegen."];
        }

        if (anfrage.BeleglisteFormat is not (Xlsx or Csv))
        {
            fehler["beleglisteFormat"] = ["Format „xlsx“ oder „csv“ wählen."];
        }

        if (bestandteilNoetig && !(anfrage.Belegliste || anfrage.SummenJeKategorie || anfrage.Vorsteuer || anfrage.ZipOriginale))
        {
            fehler["bestandteile"] = ["Mindestens einen Bestandteil wählen."];
        }

        return fehler.Count == 0 ? null : fehler;
    }

    public async Task<ExportVorschauDto> VorschauAsync(ExportAnfrageDto anfrage, CancellationToken ct)
    {
        var auswahl = await ExportAuswahl.LadenAsync(db, anfrage, ct);
        var kleinunternehmer = await IstKleinunternehmerAsync(ct);
        var kategorien = ExportTabellen.KategorieSummen(auswahl)
            .Select(k => new ExportKategorieZeileDto(k.Kategorie, k.EuerZeile ?? ExportTabellen.OhneEuerZeile, k.Brutto))
            .ToArray();

        return new ExportVorschauDto(
            auswahl.Belege.Count,
            auswahl.InEuro.Sum(b => b.Brutto ?? 0),
            kleinunternehmer ? null : ExportTabellen.VorsteuerSumme(auswahl),
            auswahl.DublettenAusgeschlossen,
            auswahl.AnzahlZuPruefen,
            anfrage.ZipOriginale ? OriginalDateien(auswahl).Sum(d => d.Groesse) : null,
            kategorien);
    }

    /// <summary>
    /// Erzeugt die Exportdatei, speichert den Lauf und protokolliert ihn. Weicht der Hash eines Originals von
    /// <see cref="BelegDatei.Sha256"/> ab (oder fehlt die Datei), entsteht nichts und die Pfade kommen zurück.
    /// Bei Kleinunternehmerregelung entfällt die Vorsteuer, auch wenn sie angefordert ist.
    /// </summary>
    public async Task<ExportErgebnis> ErzeugenAsync(ExportAnfrageDto anfrage, CancellationToken ct)
    {
        var auswahl = await ExportAuswahl.LadenAsync(db, anfrage, ct);
        var ust = await einstellungen.LesenAsync<UstEinstellungen>(ct);
        var mitVorsteuer = anfrage.Vorsteuer && ust.Status != UstStatus.Kleinunternehmer;

        var tabellen = new List<ExportTabelle>();
        if (anfrage.Belegliste)
        {
            tabellen.Add(ExportTabellen.Belegliste(auswahl));
        }

        if (anfrage.SummenJeKategorie)
        {
            tabellen.Add(ExportTabellen.SummenJeKategorie(auswahl));
        }

        if (mitVorsteuer)
        {
            tabellen.Add(ExportTabellen.Vorsteuer(auswahl, anfrage.Von, anfrage.Bis, ust.Zeitraum));
        }

        if (tabellen.Count == 0 && !anfrage.ZipOriginale)
        {
            return new ExportErgebnis(null, [], NichtsZuExportieren: true);
        }

        var originale = new List<(BelegDatei Datei, string Absolut)>();
        if (anfrage.ZipOriginale)
        {
            var ablage = Path.GetFullPath(konfiguration["Pfade:Ablage"]
                ?? throw new InvalidOperationException("Pfade:Ablage ist nicht konfiguriert."));
            originale.AddRange(OriginalDateien(auswahl).Select(d => (d, Path.Combine(ablage, d.Pfad))));
            var abweichungen = await AbweichungenAsync(originale, ct);
            if (abweichungen.Count > 0)
            {
                logger.LogWarning("Export: {Anzahl} Originale weichen vom gespeicherten Hash ab.", abweichungen.Count);
                return new ExportErgebnis(null, abweichungen);
            }
        }

        var jetzt = zeit.GetUtcNow();
        var nurExcel = !anfrage.ZipOriginale && anfrage.BeleglisteFormat == Xlsx;
        var dateiname = $"Export_{Bereinigen(anfrage.Bezeichnung)}_{jetzt:yyyyMMdd-HHmm}.{(nurExcel ? Xlsx : "zip")}";
        var ordner = Path.Combine(DatenOrdner(), "exporte");
        Directory.CreateDirectory(ordner);
        var lauf = new ExportLauf
        {
            Id = Guid.NewGuid(),
            ErstelltAm = jetzt,
            BenutzerId = benutzer.Id ?? Guid.Empty,
            BenutzerName = benutzer.Anzeigename,
            Von = anfrage.Von,
            Bis = anfrage.Bis,
            Bezeichnung = anfrage.Bezeichnung.Trim(),
            Inhalt = Inhalt(anfrage, mitVorsteuer),
            AnzahlBelege = auswahl.Belege.Count,
            Dateiname = dateiname,
        };
        lauf.Dateipfad = $"exporte/{lauf.Id:N}_{dateiname}";
        var absolut = Path.Combine(DatenOrdner(), lauf.Dateipfad);

        await using (var ziel = new FileStream(absolut, FileMode.CreateNew, FileAccess.Write))
        {
            if (nurExcel)
            {
                TabellenSchreiber.ExcelSchreiben(tabellen, ziel);
            }
            else
            {
                await ZipSchreibenAsync(ziel, tabellen, anfrage.BeleglisteFormat, originale, ct);
            }
        }

        db.ExportLaeufe.Add(lauf);
        audit.Notieren(AuditEntitaet, lauf.Id.ToString(), null, AuditAktion, string.Join("; ",
            $"Zeitraum: {anfrage.Von:yyyy-MM-dd} – {anfrage.Bis:yyyy-MM-dd}",
            $"Anzahl: {lauf.AnzahlBelege}",
            $"Inhalt: {lauf.Inhalt}",
            $"Datei: {dateiname}"));
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Export {Datei} mit {Anzahl} Belegen erzeugt.", dateiname, lauf.AnzahlBelege);
        return new ExportErgebnis(Dto(lauf), []);
    }

    public async Task<ExportLaufDto[]> LaeufeAsync(CancellationToken ct) =>
        (await db.ExportLaeufe.AsNoTracking()
            .OrderByDescending(l => l.ErstelltAm)
            .Take(AnzahlLaeufe)
            .ToListAsync(ct))
        .Select(Dto)
        .ToArray();

    /// <summary>Datei eines Laufs; <c>null</c>, wenn es den Lauf nicht gibt.</summary>
    public async Task<ExportDatei?> DateiAsync(Guid id, CancellationToken ct)
    {
        var lauf = await db.ExportLaeufe.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lauf is null)
        {
            return null;
        }

        var absolut = Path.Combine(DatenOrdner(), lauf.Dateipfad);
        return new ExportDatei(File.Exists(absolut) ? absolut : null, lauf.Dateiname);
    }

    /// <summary>„Belegliste (xlsx), Summen, Vorsteuer, ZIP“ – nur die tatsächlich enthaltenen Teile.</summary>
    public static string Inhalt(ExportAnfrageDto anfrage, bool mitVorsteuer)
    {
        var teile = new List<string>();
        if (anfrage.Belegliste)
        {
            teile.Add($"Belegliste ({anfrage.BeleglisteFormat})");
        }

        if (anfrage.SummenJeKategorie)
        {
            teile.Add("Summen");
        }

        if (mitVorsteuer)
        {
            teile.Add("Vorsteuer");
        }

        if (anfrage.ZipOriginale)
        {
            teile.Add("ZIP");
        }

        return string.Join(", ", teile);
    }

    /// <summary>Bezeichnung für den Dateinamen: nur Buchstaben, Ziffern, „-“ und „_“; Leerraum wird zu „_“.</summary>
    public static string Bereinigen(string bezeichnung)
    {
        var ohneLeerraum = Leerraum().Replace(bezeichnung.Trim(), "_");
        var bereinigt = UnerlaubtesZeichen().Replace(ohneLeerraum, "");
        bereinigt = bereinigt.Length > 80 ? bereinigt[..80] : bereinigt;
        return bereinigt.Length == 0 ? "Export" : bereinigt;
    }

    private static ExportLaufDto Dto(ExportLauf lauf) => new(
        lauf.Id,
        lauf.ErstelltAm,
        lauf.Bezeichnung,
        lauf.Inhalt,
        lauf.AnzahlBelege,
        lauf.Von,
        lauf.Bis,
        ApiRouten.Export.LaufDatei.Replace("{id}", lauf.Id.ToString(), StringComparison.Ordinal));

    /// <summary>Originale (bei ZUGFeRD auch das eingebettete XML) aller ausgewählten Belege, nach Pfad sortiert.</summary>
    private static List<BelegDatei> OriginalDateien(ExportAuswahlErgebnis auswahl) =>
        auswahl.Belege.SelectMany(b => b.Dateien)
            .Where(d => d.Rolle is DateiRolle.Original or DateiRolle.EingebettetesXml)
            .OrderBy(d => d.Pfad, StringComparer.Ordinal)
            .ToList();

    private async Task<bool> IstKleinunternehmerAsync(CancellationToken ct) =>
        (await einstellungen.LesenAsync<UstEinstellungen>(ct)).Status == UstStatus.Kleinunternehmer;

    private static async Task<List<string>> AbweichungenAsync(
        IEnumerable<(BelegDatei Datei, string Absolut)> originale,
        CancellationToken ct)
    {
        var abweichungen = new List<string>();
        foreach (var (datei, absolut) in originale)
        {
            if (!File.Exists(absolut)
                || !string.Equals(await HashAsync(absolut, ct), datei.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                abweichungen.Add(datei.Pfad);
            }
        }

        return abweichungen;
    }

    private static async Task<string> HashAsync(string pfad, CancellationToken ct)
    {
        await using var datei = File.OpenRead(pfad);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(datei, ct));
    }

    /// <summary>
    /// Tabellen als CSV-Dateien bzw. eine <c>.xlsx</c>, dazu <c>originale/</c> unter dem Ablagepfad und
    /// <c>pruefsummen.sha256</c> im Format von <c>sha256sum</c>.
    /// </summary>
    private static async Task ZipSchreibenAsync(
        Stream ziel,
        IReadOnlyList<ExportTabelle> tabellen,
        string format,
        IReadOnlyList<(BelegDatei Datei, string Absolut)> originale,
        CancellationToken ct)
    {
        using var zip = new ZipArchive(ziel, ZipArchiveMode.Create, leaveOpen: true);
        if (tabellen.Count > 0 && format == Xlsx)
        {
            // ClosedXML braucht einen Stream mit Seek; der Zip-Eintrag hat keinen.
            using var puffer = new MemoryStream();
            TabellenSchreiber.ExcelSchreiben(tabellen, puffer);
            puffer.Position = 0;
            await using var eintrag = await zip.CreateEntry("export.xlsx").OpenAsync(ct);
            await puffer.CopyToAsync(eintrag, ct);
        }
        else
        {
            foreach (var tabelle in tabellen)
            {
                await using var eintrag = await zip.CreateEntry(tabelle.Datei).OpenAsync(ct);
                TabellenSchreiber.CsvSchreiben(tabelle, eintrag);
            }
        }

        if (originale.Count == 0)
        {
            return;
        }

        var pruefsummen = new StringBuilder();
        foreach (var (datei, absolut) in originale)
        {
            var name = Originale + datei.Pfad.Replace('\\', '/');
            await using (var eintrag = await zip.CreateEntry(name, CompressionLevel.Optimal).OpenAsync(ct))
            await using (var quelle = File.OpenRead(absolut))
            {
                await quelle.CopyToAsync(eintrag, ct);
            }

            pruefsummen.Append(CultureInfo.InvariantCulture, $"{datei.Sha256.ToLowerInvariant()}  {name}\n");
        }

        await using var summen = await zip.CreateEntry(Pruefsummen).OpenAsync(ct);
        await summen.WriteAsync(Encoding.UTF8.GetBytes(pruefsummen.ToString()), ct);
    }

    private string DatenOrdner() => Path.GetFullPath(konfiguration["Pfade:Daten"]
        ?? throw new InvalidOperationException("Pfade:Daten ist nicht konfiguriert."));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Leerraum();

    [GeneratedRegex(@"[^\p{L}\p{Nd}_\-]")]
    private static partial Regex UnerlaubtesZeichen();
}
