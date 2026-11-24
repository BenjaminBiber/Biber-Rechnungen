using System.Text.Json;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.Auth;

/// <summary>Eine angemeldete Sitzung (Claim <c>sid</c> im Cookie).</summary>
public sealed record Sitzung(string Id, DateTimeOffset Zeitpunkt, string Geraet);

/// <summary>
/// Je Benutzer gespeicherte Anmeldedaten in <see cref="Einstellung"/>: die Liste der aktiven Sitzungen
/// (<c>sitzungen.&lt;BenutzerId&gt;</c>) und der Zeitpunkt der letzten Passwortänderung
/// (<c>passwort-geaendert.&lt;BenutzerId&gt;</c>).
/// </summary>
public sealed class AnmeldeSpeicher(BiberDbContext db, TimeProvider zeit)
{
    public static readonly TimeSpan AktivitaetIntervall = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string SitzungenSchluessel(Guid benutzerId) => $"sitzungen.{benutzerId}";

    public static string PasswortSchluessel(Guid benutzerId) => $"passwort-geaendert.{benutzerId}";

    public async Task<IReadOnlyList<Sitzung>> SitzungenAsync(Guid benutzerId, CancellationToken ct) =>
        await LesenAsync<List<Sitzung>>(SitzungenSchluessel(benutzerId), ct) ?? [];

    public async Task<bool> SitzungBekanntAsync(Guid benutzerId, string sitzungId, CancellationToken ct) =>
        (await SitzungenAsync(benutzerId, ct)).Any(s => s.Id == sitzungId);

    /// <summary>Legt eine neue Sitzung an und gibt ihre ID zurück.</summary>
    public async Task<string> SitzungAnlegenAsync(Guid benutzerId, string geraet, CancellationToken ct)
    {
        var sitzung = new Sitzung(Guid.NewGuid().ToString("N"), zeit.GetUtcNow(), geraet);
        await SitzungenAendernAsync(benutzerId, liste => [.. liste, sitzung], ct);
        return sitzung.Id;
    }

    public Task SitzungEntfernenAsync(Guid benutzerId, string sitzungId, CancellationToken ct) =>
        SitzungenAendernAsync(benutzerId, liste => [.. liste.Where(s => s.Id != sitzungId)], ct);

    /// <summary>Beendet alle Sitzungen außer <paramref name="sitzungId"/>.</summary>
    public Task NurSitzungBehaltenAsync(Guid benutzerId, string? sitzungId, CancellationToken ct) =>
        SitzungenAendernAsync(benutzerId, liste => [.. liste.Where(s => s.Id == sitzungId)], ct);

    public Task AlleSitzungenBeendenAsync(Guid benutzerId, CancellationToken ct) =>
        SitzungenAendernAsync(benutzerId, _ => [], ct);

    public async Task<DateTimeOffset?> PasswortGeaendertAmAsync(Guid benutzerId, CancellationToken ct) =>
        await LesenAsync<DateTimeOffset?>(PasswortSchluessel(benutzerId), ct);

    public Task PasswortGeaendertAsync(Guid benutzerId, CancellationToken ct) =>
        SchreibenAsync(PasswortSchluessel(benutzerId), zeit.GetUtcNow(), ct);

    /// <summary>
    /// Setzt <see cref="Benutzer.LetzteAktivitaetAm"/> auf jetzt – ohne <paramref name="erzwingen"/>
    /// höchstens einmal je <see cref="AktivitaetIntervall"/>.
    /// </summary>
    public async Task AktivitaetMerkenAsync(Guid benutzerId, bool erzwingen, CancellationToken ct)
    {
        var jetzt = zeit.GetUtcNow();
        var grenze = erzwingen ? DateTimeOffset.MaxValue : jetzt - AktivitaetIntervall;
        await db.Users
            .Where(u => u.Id == benutzerId && (u.LetzteAktivitaetAm == null || u.LetzteAktivitaetAm <= grenze))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LetzteAktivitaetAm, jetzt), ct);
    }

    private async Task SitzungenAendernAsync(
        Guid benutzerId, Func<List<Sitzung>, List<Sitzung>> aendern, CancellationToken ct)
    {
        var schluessel = SitzungenSchluessel(benutzerId);
        var liste = await LesenAsync<List<Sitzung>>(schluessel, ct) ?? [];
        await SchreibenAsync(schluessel, aendern(liste), ct);
    }

    private async Task<T?> LesenAsync<T>(string schluessel, CancellationToken ct)
    {
        var wert = await db.Einstellungen.AsNoTracking()
            .Where(e => e.Schluessel == schluessel)
            .Select(e => e.Wert)
            .SingleOrDefaultAsync(ct);
        return wert is null ? default : JsonSerializer.Deserialize<T>(wert, Json);
    }

    private async Task SchreibenAsync<T>(string schluessel, T wert, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(wert, Json);
        var jetzt = zeit.GetUtcNow();
        var geaendert = await db.Einstellungen
            .Where(e => e.Schluessel == schluessel)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Wert, json).SetProperty(e => e.GeaendertAm, jetzt), ct);
        if (geaendert == 0)
        {
            db.Einstellungen.Add(new Einstellung { Schluessel = schluessel, Wert = json, GeaendertAm = jetzt });
            await db.SaveChangesAsync(ct);
        }
    }
}
