using System.Globalization;
using System.Security.Cryptography;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Biber.Invoice.Infrastructure.Module.Integritaet;

/// <summary>
/// Vergleicht die SHA-256-Prüfsummen aller Originale und eingebetteten XML unter <c>Pfade:Ablage</c> mit den
/// gespeicherten Werten, protokolliert jeden Lauf als <see cref="IntegritaetsLauf"/> und meldet das Ergebnis.
/// </summary>
public sealed class IntegritaetsPruefer(
    BiberDbContext db,
    IBenachrichtigungsDienst benachrichtigungen,
    IAuditSchreiber audit,
    IConfiguration konfiguration,
    TimeProvider zeit,
    ILogger<IntegritaetsPruefer> logger)
{
    public const string Aktion = "IntegritaetAbweichung";
    public const string Link = "/einstellungen/integritaet";
    public const int Blockgroesse = 200;

    /// <summary>Ein Lauf ohne Ende, der älter ist, gilt als abgebrochen und sperrt keinen neuen Lauf.</summary>
    public static readonly TimeSpan MaxLaufzeit = TimeSpan.FromHours(6);

    private static readonly SemaphoreSlim Sperre = new(1, 1);
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Läuft gerade eine Prüfung (begonnen, nicht beendet, jünger als <see cref="MaxLaufzeit"/>)?</summary>
    public async Task<bool> LaeuftAsync(CancellationToken ct)
    {
        var grenze = zeit.GetUtcNow() - MaxLaufzeit;
        return await db.IntegritaetsLaeufe.AnyAsync(l => l.BeendetAm == null && l.GestartetAm > grenze, ct);
    }

    /// <summary>Prüft alle Dateien; <c>null</c>, wenn bereits ein Lauf läuft.</summary>
    public async Task<IntegritaetsLauf?> PruefenAsync(CancellationToken ct)
    {
        if (!await Sperre.WaitAsync(0, ct))
        {
            return null;
        }

        try
        {
            if (await LaeuftAsync(ct))
            {
                return null;
            }

            return await DurchfuehrenAsync(ct);
        }
        finally
        {
            Sperre.Release();
        }
    }

    private async Task<IntegritaetsLauf> DurchfuehrenAsync(CancellationToken ct)
    {
        var wurzel = Path.GetFullPath(konfiguration["Pfade:Ablage"]
            ?? throw new InvalidOperationException("Pfade:Ablage ist nicht konfiguriert."));
        var lauf = new IntegritaetsLauf { Id = Guid.NewGuid(), GestartetAm = zeit.GetUtcNow() };
        db.IntegritaetsLaeufe.Add(lauf);
        await db.SaveChangesAsync(ct);

        while (true)
        {
            var block = await db.BelegDateien
                .Where(d => d.Rolle == DateiRolle.Original || d.Rolle == DateiRolle.EingebettetesXml)
                .OrderBy(d => d.Id)
                .Skip(lauf.AnzahlDateien)
                .Take(Blockgroesse)
                .ToListAsync(ct);
            if (block.Count == 0)
            {
                break;
            }

            foreach (var datei in block)
            {
                var ok = await StimmtAsync(Path.Combine(wurzel, datei.Pfad), datei.Sha256, ct);
                datei.LetztePruefungAm = zeit.GetUtcNow();
                datei.PruefErgebnis = ok;
                lauf.AnzahlDateien++;
                if (!ok)
                {
                    lauf.Abweichungen.Add(datei.Pfad);
                    audit.Notieren(nameof(BelegDatei), datei.Id.ToString(), datei.BelegId, Aktion, datei.Pfad);
                    logger.LogWarning("Integritätsprüfung: {Pfad} weicht von der Prüfsumme ab.", datei.Pfad);
                }
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        lauf.AnzahlAbweichungen = lauf.Abweichungen.Count;
        lauf.BeendetAm = zeit.GetUtcNow();
        db.IntegritaetsLaeufe.Update(lauf);
        await db.SaveChangesAsync(ct);

        await benachrichtigungen.MeldenAsync(Meldung(lauf), ct);
        return lauf;
    }

    private NeueBenachrichtigung Meldung(IntegritaetsLauf lauf)
    {
        if (lauf.AnzahlAbweichungen == 0)
        {
            return new NeueBenachrichtigung(
                BenachrichtigungArt.IntegritaetOk,
                Schweregrad.Ok,
                "Integritätsprüfung ohne Befund",
                $"{Zahl(lauf.AnzahlDateien)} Dateien geprüft, alle Prüfsummen stimmen.",
                Link,
                EntdoppelungsSchluessel: $"integritaet-ok-{lauf.GestartetAm:yyyy-MM-dd}");
        }

        var text = lauf.AnzahlAbweichungen == 1
            ? $"1 Datei weicht von ihrer Prüfsumme ab: {lauf.Abweichungen[0]}"
            : $"{Zahl(lauf.AnzahlAbweichungen)} Dateien weichen von ihrer Prüfsumme ab: {string.Join(", ", lauf.Abweichungen.Take(3))}";
        return new NeueBenachrichtigung(
            BenachrichtigungArt.Integritaetsfehler,
            Schweregrad.Fehler,
            "Integritätsfehler in der Ablage",
            text,
            Link,
            NurAdmins: true,
            EntdoppelungsSchluessel: $"integritaet-{lauf.Id}");
    }

    internal static string Zahl(int wert) => wert.ToString("N0", Deutsch);

    private async Task<bool> StimmtAsync(string pfad, string erwartet, CancellationToken ct)
    {
        try
        {
            await using var strom = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(strom, ct));
            return string.Equals(hash, erwartet, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
