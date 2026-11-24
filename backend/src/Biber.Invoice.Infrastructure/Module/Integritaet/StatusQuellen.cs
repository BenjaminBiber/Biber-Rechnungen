using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Auswertung;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Integritaet;

/// <summary>Ergebnis der letzten abgeschlossenen Integritätsprüfung für <c>GET system/status</c>.</summary>
public sealed class IntegritaetsStatus(BiberDbContext db, TimeProvider zeit) : ISystemStatusQuelle
{
    public string Schluessel => "integritaet";

    public async Task<SystemStatusEintragDto> LesenAsync(CancellationToken ct)
    {
        var lauf = await db.IntegritaetsLaeufe.AsNoTracking()
            .Where(l => l.BeendetAm != null)
            .OrderByDescending(l => l.GestartetAm)
            .FirstOrDefaultAsync(ct);
        if (lauf is null)
        {
            return Eintrag(Schweregrad.Info, "Noch keine Integritätsprüfung", "Die Prüfung läuft nachts um 03:00.");
        }

        var wann = Zeitangabe.Formatieren(lauf.GestartetAm, zeit.GetUtcNow());
        return lauf.AnzahlAbweichungen == 0
            ? Eintrag(Schweregrad.Ok, "Integritätsprüfung ohne Befund",
                $"{IntegritaetsPruefer.Zahl(lauf.AnzahlDateien)} Dateien, SHA-256 geprüft · {wann}")
            : Eintrag(Schweregrad.Fehler, "Integritätsfehler in der Ablage",
                $"{IntegritaetsPruefer.Zahl(lauf.AnzahlAbweichungen)} von {IntegritaetsPruefer.Zahl(lauf.AnzahlDateien)} Dateien weichen ab · {wann}");
    }

    private SystemStatusEintragDto Eintrag(Schweregrad schweregrad, string titel, string text) =>
        new(Schluessel, schweregrad, titel, text, zeit.GetUtcNow());
}

/// <summary>Stand des Datenbank-Backups für <c>GET system/status</c>; älter als 26 Stunden gilt als veraltet.</summary>
public sealed class BackupStatus(BackupStatusLeser leser, TimeProvider zeit) : ISystemStatusQuelle
{
    public static readonly TimeSpan MaxAlter = TimeSpan.FromHours(26);

    public string Schluessel => "backup";

    public Task<SystemStatusEintragDto> LesenAsync(CancellationToken ct)
    {
        var stand = leser.Lesen();
        var jetzt = zeit.GetUtcNow();
        if (stand.LetztesBackup is not { } letztes)
        {
            return Task.FromResult(Eintrag(Schweregrad.Warnung, "Kein aktuelles Datenbank-Backup", "Kein Dump im Backup-Ordner gefunden"));
        }

        var text = $"pg_dump {Zeitangabe.Formatieren(letztes, jetzt)} · {IntegritaetsPruefer.Zahl(stand.AnzahlDumps)} Dumps vorhanden";
        return Task.FromResult(jetzt - letztes > MaxAlter
            ? Eintrag(Schweregrad.Warnung, "Kein aktuelles Datenbank-Backup", text)
            : Eintrag(Schweregrad.Ok, "Datenbank-Backup erstellt", text));
    }

    private SystemStatusEintragDto Eintrag(Schweregrad schweregrad, string titel, string text) =>
        new(Schluessel, schweregrad, titel, text, zeit.GetUtcNow());
}

/// <summary>„heute 03:00“, „gestern 03:00“ oder „14.01.2026 03:00“ in deutscher Ortszeit.</summary>
internal static class Zeitangabe
{
    private static readonly TimeZoneInfo Zone = Lade();

    public static string Formatieren(DateTimeOffset zeitpunkt, DateTimeOffset jetzt)
    {
        var lokal = TimeZoneInfo.ConvertTime(zeitpunkt, Zone);
        var heute = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(jetzt, Zone).DateTime);
        var tag = DateOnly.FromDateTime(lokal.DateTime);
        var uhrzeit = lokal.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        if (tag == heute)
        {
            return $"heute {uhrzeit}";
        }

        return tag == heute.AddDays(-1)
            ? $"gestern {uhrzeit}"
            : $"{lokal.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture)} {uhrzeit}";
    }

    private static TimeZoneInfo Lade()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
