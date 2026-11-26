using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Biber.Invoice.Infrastructure.Module.Mail;

public sealed record MailAbrufErgebnis(int Mails, int Anhaenge, int OhneBeleg);

/// <summary>Der Abruf ist gescheitert; <see cref="Exception.Message"/> ist für Benutzer lesbar und enthält nie das Passwort.</summary>
public sealed class MailAbrufFehler(string meldung, Exception ursache) : Exception(meldung, ursache);

/// <summary>
/// Ruft das Postfach per IMAP ab: Verwertbare Anhänge gehen an <see cref="IImportDienst"/>, die Mail wandert danach
/// nach <see cref="MailEinstellungen.VerarbeitetOrdner"/>, ohne Anhang nach <see cref="MailEinstellungen.OhneBelegOrdner"/>.
/// Mails werden nie gelöscht. Nie laufen zwei Abrufe gleichzeitig.
/// </summary>
public sealed class MailAbruf(
    IServiceScopeFactory scopes,
    IOptionsMonitor<ImapZugang> zugang,
    TimeProvider zeit,
    ILogger<MailAbruf> log)
{
    public const int HoechstensJeLauf = 50;
    public const int MindestgroesseBild = 10 * 1024;
    public const string ErgebnisOhneBeleg = "Mail → Ohne Beleg";
    public const string NichtEingerichtet = "E-Mail-Import nicht eingerichtet.";

    private static readonly HashSet<string> ErlaubteEndungen =
        new([".pdf", ".xml", ".png", ".jpg", ".jpeg", ".tif", ".tiff"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> BildEndungen =
        new([".png", ".jpg", ".jpeg", ".tif", ".tiff"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Endungen, die auch als <c>inline</c> markiert als Beleg zählen.</summary>
    private static readonly HashSet<string> InlineEndungen = new([".pdf", ".xml"], StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _sperre = new(1, 1);

    public bool Konfiguriert => zugang.CurrentValue.Konfiguriert;

    /// <summary>Ruft ab; läuft gerade ein Abruf, wird auf ihn gewartet.</summary>
    /// <exception cref="MailAbrufFehler">Verbindung, Anmeldung oder Eingangsordner scheitern.</exception>
    public async Task<MailAbrufErgebnis> AbrufenAsync(CancellationToken ct)
    {
        await _sperre.WaitAsync(ct);
        try
        {
            return await AbrufenGesperrtAsync(ct);
        }
        finally
        {
            _sperre.Release();
        }
    }

    /// <returns><c>null</c>, wenn gerade ein anderer Abruf läuft.</returns>
    /// <exception cref="MailAbrufFehler">Verbindung, Anmeldung oder Eingangsordner scheitern.</exception>
    public async Task<MailAbrufErgebnis?> AbrufenFallsFreiAsync(CancellationToken ct)
    {
        if (!await _sperre.WaitAsync(0, ct))
        {
            return null;
        }

        try
        {
            return await AbrufenGesperrtAsync(ct);
        }
        finally
        {
            _sperre.Release();
        }
    }

    /// <summary>Meldet an und öffnet den Eingangsordner, ohne etwas zu verändern.</summary>
    public async Task<AktionErgebnisDto> VerbindungTestenAsync(CancellationToken ct)
    {
        var z = zugang.CurrentValue;
        if (!z.Konfiguriert)
        {
            return new AktionErgebnisDto(false, NichtEingerichtet);
        }

        try
        {
            var einstellungen = await EinstellungenAsync(ct);
            using var client = await VerbindenAsync(z, ct);
            var eingang = await OrdnerAsync(client, einstellungen.Eingangsordner, anlegen: false, ct);
            await eingang.OpenAsync(FolderAccess.ReadOnly, ct);
            var anzahl = eingang.Count;
            await client.DisconnectAsync(true, ct);
            await FehlerVerwerfenAsync(ct);
            return new AktionErgebnisDto(true, $"Verbindung erfolgreich · {Anzahl(anzahl, "Mail", "Mails")} im Eingang");
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException)
        {
            var meldung = Meldung(fehler, z);
            log.LogWarning("Verbindungstest zum Postfach fehlgeschlagen: {Meldung}", meldung);
            return new AktionErgebnisDto(false, meldung);
        }
    }

    public static string Anzahl(int anzahl, string einzahl, string mehrzahl) => $"{anzahl} {(anzahl == 1 ? einzahl : mehrzahl)}";

    private async Task<MailAbrufErgebnis> AbrufenGesperrtAsync(CancellationToken ct)
    {
        var beginn = zeit.GetUtcNow();
        var z = zugang.CurrentValue;
        MailAbrufErgebnis ergebnis;
        try
        {
            if (!z.Konfiguriert)
            {
                throw new InvalidOperationException(NichtEingerichtet);
            }

            ergebnis = await AbholenAsync(z, ct);
        }
        catch (Exception fehler) when (fehler is not OperationCanceledException)
        {
            var meldung = Meldung(fehler, z);
            log.LogWarning("Mail-Abruf fehlgeschlagen: {Meldung}", meldung);
            await StatusSchreibenAsync(new MailAbrufStatus(beginn, meldung, 0, 0, 0), ct);
            throw new MailAbrufFehler(meldung, fehler);
        }

        await StatusSchreibenAsync(new MailAbrufStatus(beginn, null, ergebnis.Mails, ergebnis.Anhaenge, ergebnis.OhneBeleg), ct);
        log.LogInformation(
            "Mail-Abruf: {Mails} Mails, {Anhaenge} Anhänge übernommen, {OhneBeleg} ohne Beleg",
            ergebnis.Mails,
            ergebnis.Anhaenge,
            ergebnis.OhneBeleg);
        return ergebnis;
    }

    private async Task<MailAbrufErgebnis> AbholenAsync(ImapZugang z, CancellationToken ct)
    {
        var einstellungen = await EinstellungenAsync(ct);
        using var client = await VerbindenAsync(z, ct);
        var verarbeitet = await OrdnerAsync(client, einstellungen.VerarbeitetOrdner, anlegen: true, ct);
        var ohneBeleg = await OrdnerAsync(client, einstellungen.OhneBelegOrdner, anlegen: true, ct);
        var eingang = await OrdnerAsync(client, einstellungen.Eingangsordner, anlegen: false, ct);
        await eingang.OpenAsync(FolderAccess.ReadWrite, ct);

        // UIDs steigen mit der Ankunft – aufsteigend sortiert kommt die älteste Mail zuerst.
        var uids = (await eingang.SearchAsync(SearchQuery.All, ct)).OrderBy(u => u.Id).Take(HoechstensJeLauf).ToList();

        int mails = 0, anhaenge = 0, ohne = 0;
        foreach (var uid in uids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var neu = await MailVerarbeitenAsync(eingang, uid, ct);
                await eingang.MoveToAsync(uid, neu is null ? ohneBeleg : verarbeitet, ct);
                mails++;
                if (neu is null)
                {
                    ohne++;
                }
                else
                {
                    anhaenge += neu.Value;
                }
            }
            catch (Exception fehler) when (fehler is not OperationCanceledException)
            {
                log.LogWarning(fehler, "Mail {Uid} konnte nicht verarbeitet werden; sie bleibt im Eingang", uid.Id);
            }
        }

        await client.DisconnectAsync(true, ct);
        return new MailAbrufErgebnis(mails, anhaenge, ohne);
    }

    /// <returns>Anzahl neu angenommener Anhänge oder <c>null</c>, wenn die Mail keinen verwertbaren Anhang hat.</returns>
    private async Task<int?> MailVerarbeitenAsync(IMailFolder eingang, UniqueId uid, CancellationToken ct)
    {
        var nachricht = await eingang.GetMessageAsync(uid, ct);
        var absender = Kuerzen(nachricht.From.Mailboxes.FirstOrDefault()?.Address, 500);
        var betreff = Kuerzen(nachricht.Subject, 1000);
        var schluessel = Schluessel(nachricht, absender, betreff);

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();

        // Idempotenz: Was zu dieser Mail schon angenommen ist (z. B. vor einem Absturz vor dem Verschieben), nicht erneut annehmen.
        var vorhanden = (await db.ImportVorgaenge.AsNoTracking()
                .Where(v => v.Quelle == ImportQuelle.Mail && v.MailMessageId == schluessel)
                .Select(v => v.Dateiname)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var anhaenge = await AnhaengeAsync(nachricht, ct);
        try
        {
            if (anhaenge.Count == 0)
            {
                if (!vorhanden.Contains(""))
                {
                    var jetzt = zeit.GetUtcNow();
                    db.ImportVorgaenge.Add(new ImportVorgang
                    {
                        Id = Guid.NewGuid(),
                        Quelle = ImportQuelle.Mail,
                        Status = ImportStatus.OhneBeleg,
                        Dateiname = "",
                        MailMessageId = schluessel,
                        MailAbsender = absender,
                        MailBetreff = betreff,
                        Ergebnis = ErgebnisOhneBeleg,
                        ErstelltAm = jetzt,
                        BeendetAm = jetzt,
                    });
                    await db.SaveChangesAsync(ct);
                }

                return null;
            }

            var import = scope.ServiceProvider.GetRequiredService<IImportDienst>();
            var herkunft = new ImportHerkunft(MailMessageId: schluessel, MailAbsender: absender, MailBetreff: betreff);
            var neu = 0;
            foreach (var (name, inhalt) in anhaenge)
            {
                if (vorhanden.Contains(name))
                {
                    continue;
                }

                inhalt.Position = 0;
                await import.AnnehmenAsync(inhalt, name, ImportQuelle.Mail, herkunft, ct);
                neu++;
            }

            return neu;
        }
        finally
        {
            foreach (var (_, inhalt) in anhaenge)
            {
                await inhalt.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Verwertbar sind Anhänge – und als <c>inline</c> markierte PDF/XML – mit erlaubter Endung;
    /// Bilder unter <see cref="MindestgroesseBild"/> sind Signatur-Logos und zählen nicht.
    /// Gleichnamige Anhänge einer Mail werden durchnummeriert, damit jeder für sich idempotent ist.
    /// </summary>
    private static async Task<List<(string Name, MemoryStream Inhalt)>> AnhaengeAsync(MimeMessage nachricht, CancellationToken ct)
    {
        var liste = new List<(string Name, MemoryStream Inhalt)>();
        var namen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var teil in nachricht.BodyParts.OfType<MimePart>())
        {
            var name = Path.GetFileName(teil.FileName ?? "").Trim();
            var endung = Path.GetExtension(name);
            if (name.Length == 0 || teil.Content is null || !ErlaubteEndungen.Contains(endung)
                || (!teil.IsAttachment && !InlineEndungen.Contains(endung)))
            {
                continue;
            }

            var inhalt = new MemoryStream();
            await teil.Content.DecodeToAsync(inhalt, ct);
            if (BildEndungen.Contains(endung) && inhalt.Length < MindestgroesseBild)
            {
                await inhalt.DisposeAsync();
                continue;
            }

            liste.Add((Eindeutig(name, namen), inhalt));
        }

        return liste;
    }

    private static string Eindeutig(string name, HashSet<string> namen)
    {
        var kandidat = name;
        for (var i = 2; !namen.Add(kandidat); i++)
        {
            kandidat = $"{Path.GetFileNameWithoutExtension(name)} ({i}){Path.GetExtension(name)}";
        }

        return kandidat;
    }

    /// <summary><c>Message-Id</c> der Mail; fehlt sie, ein Ersatzschlüssel aus Datum, Absender und Betreff.</summary>
    private static string Schluessel(MimeMessage nachricht, string? absender, string? betreff)
    {
        if (!string.IsNullOrWhiteSpace(nachricht.MessageId))
        {
            return Kuerzen(nachricht.MessageId, 1000)!;
        }

        var roh = $"{nachricht.Date.ToUniversalTime():O}|{absender}|{betreff}";
        return "ersatz:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(roh)));
    }

    private static string? Kuerzen(string? text, int laenge) => text is { Length: var l } && l > laenge ? text[..laenge] : text;

    private static async Task<ImapClient> VerbindenAsync(ImapZugang z, CancellationToken ct)
    {
        var client = new ImapClient { Timeout = (int)TimeSpan.FromSeconds(60).TotalMilliseconds };
        try
        {
            var sicherheit = z.Ssl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(z.Host!, z.Port, sicherheit, ct);
            await client.AuthenticateAsync(z.Benutzer!, z.Passwort!, ct);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task<IMailFolder> OrdnerAsync(ImapClient client, string name, bool anlegen, CancellationToken ct)
    {
        if (string.Equals(name, "INBOX", StringComparison.OrdinalIgnoreCase))
        {
            return client.Inbox;
        }

        try
        {
            return await client.GetFolderAsync(name, ct);
        }
        catch (FolderNotFoundException) when (anlegen)
        {
            var wurzel = client.GetFolder(client.PersonalNamespaces[0]);
            log.LogInformation("Lege Postfach-Ordner {Ordner} an", name);
            return await wurzel.CreateAsync(name, true, ct)
                ?? throw new InvalidOperationException($"Ordner „{name}“ ließ sich im Postfach nicht anlegen.");
        }
    }

    private async Task<MailEinstellungen> EinstellungenAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IEinstellungen>().LesenAsync<MailEinstellungen>(ct);
    }

    /// <summary>Ein alter Fehler ist nach gelungener Anmeldung überholt; der nächste Job-Lauf prüft den Abruf neu.</summary>
    private async Task FehlerVerwerfenAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        if (await MailStatusSpeicher.FehlerVerwerfenAsync(scope.ServiceProvider.GetRequiredService<BiberDbContext>(), ct))
        {
            log.LogInformation("Verbindungstest gelungen; alter Fehler des Mail-Abrufs verworfen");
        }
    }

    private async Task StatusSchreibenAsync(MailAbrufStatus status, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await MailStatusSpeicher.SchreibenAsync(scope.ServiceProvider.GetRequiredService<BiberDbContext>(), status, zeit.GetUtcNow(), ct);
    }

    /// <summary>Lesbare Fehlermeldung; das Passwort wird vorsichtshalber herausgefiltert.</summary>
    private static string Meldung(Exception fehler, ImapZugang z)
    {
        var meldung = fehler switch
        {
            AuthenticationException => "Anmeldung am Postfach fehlgeschlagen – Benutzer oder Passwort falsch.",
            FolderNotFoundException f => $"Ordner „{f.FolderName}“ gibt es im Postfach nicht.",
            SocketException or SslHandshakeException or ImapProtocolException or IOException or TimeoutException =>
                $"Postfach nicht erreichbar: {fehler.Message}",
            _ => fehler.Message,
        };
        return string.IsNullOrEmpty(z.Passwort) ? meldung : meldung.Replace(z.Passwort, "***", StringComparison.Ordinal);
    }
}
