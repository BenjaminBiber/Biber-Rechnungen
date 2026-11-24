using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>Gemeinsamer Eingang der Pipeline: prüft das Format, staged die Datei und plant die Verarbeitung ein.</summary>
public sealed class ImportDienst(
    BiberDbContext db,
    IStagingSpeicher staging,
    IVerarbeitungsPlaner planer,
    IEnumerable<IPipelineSchritt> schritte,
    IEnumerable<IBenachrichtigungsDienst> benachrichtigungen,
    TimeProvider zeit,
    ILogger<ImportDienst> log) : IImportDienst
{
    public const string FormatNichtUnterstuetzt = "Format nicht unterstützt. Erlaubt sind PDF, XML, PNG, JPG und TIFF.";
    public const int Seitengroesse = 50;

    private static readonly HashSet<string> ErlaubteEndungen =
        new([".pdf", ".xml", ".png", ".jpg", ".jpeg", ".tif", ".tiff"], StringComparer.OrdinalIgnoreCase);

    public async Task<ImportVorgangDto> AnnehmenAsync(Stream inhalt, string dateiname, ImportQuelle quelle, ImportHerkunft herkunft, CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var vorgang = new ImportVorgang
        {
            Id = Guid.NewGuid(),
            Quelle = quelle,
            Dateiname = Path.GetFileName(dateiname),
            HerkunftPfad = herkunft.Pfad,
            MailMessageId = herkunft.MailMessageId,
            MailAbsender = herkunft.MailAbsender,
            MailBetreff = herkunft.MailBetreff,
            BenutzerId = herkunft.BenutzerId,
            ApiTokenId = herkunft.ApiTokenId,
            ErstelltAm = jetzt,
        };

        if (!ErlaubteEndungen.Contains(Path.GetExtension(dateiname)))
        {
            vorgang.Status = ImportStatus.Abgelehnt;
            vorgang.Fehler = FormatNichtUnterstuetzt;
            vorgang.Ergebnis = "Format nicht unterstützt";
            vorgang.Groesse = inhalt.CanSeek ? inhalt.Length : 0;
            vorgang.BeendetAm = jetzt;
            db.ImportVorgaenge.Add(vorgang);
            await db.SaveChangesAsync(ct);
            log.LogInformation("Import {Vorgang} abgelehnt: Endung {Endung} nicht unterstützt", vorgang.Id, Path.GetExtension(dateiname));
            await benachrichtigungen.MeldenAsync(
                new NeueBenachrichtigung(
                    BenachrichtigungArt.ImportFehler,
                    Schweregrad.Fehler,
                    "Import abgelehnt",
                    $"{vorgang.Dateiname}: {FormatNichtUnterstuetzt}",
                    Link: "/hochladen"),
                ct);
            return await AbbildenAsync(vorgang, ct);
        }

        var datei = await staging.SpeichernAsync(inhalt, dateiname, ct);
        vorgang.Status = ImportStatus.Angenommen;
        vorgang.StagingPfad = datei.Pfad;
        vorgang.Sha256 = datei.Sha256;
        vorgang.Groesse = datei.Groesse;
        db.ImportVorgaenge.Add(vorgang);

        // ImportVorgang hat kein Feld für die Herkunft einer Aufteilung; der Beleg trägt sie deshalb von Anfang an.
        if (herkunft.AufgeteiltAusBelegId is { } aufgeteiltAus)
        {
            var beleg = VerarbeitungsJob.NeuerBeleg(vorgang, jetzt);
            beleg.Status = BelegStatus.Importiert;
            beleg.AufgeteiltAusBelegId = aufgeteiltAus;
            db.Belege.Add(beleg);
            vorgang.BelegId = beleg.Id;
        }

        await db.SaveChangesAsync(ct);
        planer.Einplanen(vorgang.Id);
        return await AbbildenAsync(vorgang, ct);
    }

    public async Task<SeiteDto<ImportVorgangDto>> VorgaengeAsync(ImportQuelle? quelle, int tage, int seite, CancellationToken ct)
    {
        seite = Math.Max(seite, 1);
        var ab = zeit.GetUtcNow().AddDays(-Math.Max(tage, 0));
        var abfrage = db.ImportVorgaenge.AsNoTracking().Where(v => v.ErstelltAm >= ab);
        if (quelle is { } q)
        {
            abfrage = abfrage.Where(v => v.Quelle == q);
        }

        var gesamt = await abfrage.CountAsync(ct);
        var vorgaenge = await abfrage
            .OrderByDescending(v => v.ErstelltAm)
            .ThenBy(v => v.Id)
            .Skip((seite - 1) * Seitengroesse)
            .Take(Seitengroesse)
            .ToListAsync(ct);
        return new SeiteDto<ImportVorgangDto>(await AbbildenAsync(vorgaenge, ct), gesamt, seite, Seitengroesse);
    }

    public async Task<ImportVorgangDto?> VorgangAsync(Guid id, CancellationToken ct)
    {
        var vorgang = await db.ImportVorgaenge.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        return vorgang is null ? null : await AbbildenAsync(vorgang, ct);
    }

    public async Task<int> PausierteFortsetzenAsync(CancellationToken ct)
    {
        var pausiert = await db.ImportVorgaenge.Where(v => v.Status == ImportStatus.Pausiert).ToListAsync(ct);
        foreach (var vorgang in pausiert)
        {
            vorgang.Status = ImportStatus.Angenommen;
            vorgang.NaechsterVersuchAm = null;
        }

        await db.SaveChangesAsync(ct);
        foreach (var vorgang in pausiert)
        {
            planer.Einplanen(vorgang.Id);
        }

        return pausiert.Count;
    }

    private async Task<ImportVorgangDto> AbbildenAsync(ImportVorgang vorgang, CancellationToken ct) =>
        (await AbbildenAsync([vorgang], ct))[0];

    private async Task<IReadOnlyList<ImportVorgangDto>> AbbildenAsync(IReadOnlyList<ImportVorgang> vorgaenge, CancellationToken ct)
    {
        var benutzerIds = vorgaenge.Where(v => v.BenutzerId is not null).Select(v => v.BenutzerId!.Value).Distinct().ToList();
        var benutzer = await db.Users.AsNoTracking()
            .Where(b => benutzerIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Anzeigename, ct);

        var tokenIds = vorgaenge.Where(v => v.ApiTokenId is not null).Select(v => v.ApiTokenId!.Value).Distinct().ToList();
        var tokens = await db.ApiTokens.AsNoTracking()
            .Where(t => tokenIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var belegIds = vorgaenge.Where(v => v.BelegId is not null).Select(v => v.BelegId!.Value).Distinct().ToList();
        var duplikate = await db.Belege.AsNoTracking()
            .Where(b => belegIds.Contains(b.Id) && b.DuplikatVonId != null)
            .ToDictionaryAsync(b => b.Id, b => b.DuplikatVonId, ct);

        var vorlage = schritte.OrderBy(s => s.Reihenfolge).Select(s => new ImportSchrittDto(s.Name, VerarbeitungsJob.SchrittOffen)).ToArray();

        return vorgaenge.Select(v =>
        {
            var (herkunft, detail) = v.Quelle switch
            {
                ImportQuelle.Upload => (v.Dateiname, v.BenutzerId is { } b ? benutzer.GetValueOrDefault(b) : null),
                ImportQuelle.Ordner => (v.Dateiname, "Rechnungen-Eingang"),
                ImportQuelle.Mail => (v.MailBetreff is null ? v.Dateiname : $"„{v.MailBetreff}“", v.MailAbsender is null ? null : $"von {v.MailAbsender}"),
                ImportQuelle.Mcp => (v.Dateiname, v.ApiTokenId is { } t && tokens.TryGetValue(t, out var name) ? $"Token „{name}“" : null),
                _ => (v.Dateiname, (string?)null),
            };

            return new ImportVorgangDto(
                v.Id,
                v.ErstelltAm,
                v.Quelle,
                v.Dateiname,
                v.Groesse,
                herkunft,
                detail,
                v.Status,
                v.Versuche,
                VerarbeitungsJob.MaxVersuche,
                v.Ergebnis,
                v.Fehler,
                v.BelegId,
                v.BelegId is { } belegId ? duplikate.GetValueOrDefault(belegId) : null,
                v.NaechsterVersuchAm,
                v.Schritte.Count > 0 ? [.. v.Schritte.Select(s => new ImportSchrittDto(s.Name, s.Status))] : vorlage);
        }).ToList();
    }
}
