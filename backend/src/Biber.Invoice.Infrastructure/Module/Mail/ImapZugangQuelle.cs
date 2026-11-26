using System.Security.Cryptography;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Infrastructure.Module.Einstellungen;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Biber.Invoice.Infrastructure.Module.Mail;

/// <summary>
/// Im Frontend gepflegter IMAP-Zugang (Einstellung <c>imap</c>). Gilt nur, solange <c>Imap:Host</c> nicht gesetzt ist.
/// </summary>
public sealed record ImapEinstellungen
{
    public string? Host { get; init; }

    public int Port { get; init; } = 993;

    public bool Ssl { get; init; } = true;

    public string? Benutzer { get; init; }

    /// <summary>Mit Data Protection verschlüsselt (<see cref="ImapZugangQuelle.Zweck"/>).</summary>
    [Vertraulich]
    public string? PasswortVerschluesselt { get; init; }
}

/// <summary>
/// Liefert den wirksamen IMAP-Zugang: aus der Konfiguration (<c>Imap:*</c>, per <c>.env</c>), sobald <c>Imap:Host</c>
/// gesetzt ist – sonst aus <see cref="ImapEinstellungen"/>. Das gespeicherte Passwort ist verschlüsselt.
/// </summary>
public sealed class ImapZugangQuelle(
    IOptionsMonitor<ImapZugang> konfiguration,
    IServiceScopeFactory scopes,
    IDataProtectionProvider schutz,
    ILogger<ImapZugangQuelle> log)
{
    public const string Zweck = "Biber.Invoice.Mail.ImapPasswort";

    private readonly IDataProtector _schutz = schutz.CreateProtector(Zweck);

    /// <summary>Der Zugang kommt aus der <c>.env</c> und lässt sich im Frontend nicht ändern.</summary>
    public bool AusUmgebung => !string.IsNullOrWhiteSpace(konfiguration.CurrentValue.Host);

    /// <summary>Wirksamer Zugang mit Klartext-Passwort; nur für den Abruf, nie ausliefern oder protokollieren.</summary>
    public async Task<ImapZugang> LesenAsync(CancellationToken ct)
    {
        if (AusUmgebung)
        {
            return konfiguration.CurrentValue;
        }

        using var scope = scopes.CreateScope();
        var e = await scope.ServiceProvider.GetRequiredService<IEinstellungen>().LesenAsync<ImapEinstellungen>(ct);
        return new ImapZugang
        {
            Host = e.Host,
            Port = e.Port,
            Ssl = e.Ssl,
            Benutzer = e.Benutzer,
            Passwort = Entschluesseln(e.PasswortVerschluesselt),
        };
    }

    /// <summary>Beschreibung für das Frontend – ohne Passwort.</summary>
    public async Task<MailZugangDto> BeschreibenAsync(IEinstellungen einstellungen, CancellationToken ct)
    {
        if (AusUmgebung)
        {
            var z = konfiguration.CurrentValue;
            return new MailZugangDto(true, z.Host, z.Port, z.Ssl, Leer(z.Benutzer), !string.IsNullOrWhiteSpace(z.Passwort), z.Konfiguriert);
        }

        var e = await einstellungen.LesenAsync<ImapEinstellungen>(ct);
        var passwort = Entschluesseln(e.PasswortVerschluesselt);
        var konfiguriert = !string.IsNullOrWhiteSpace(e.Host) && !string.IsNullOrWhiteSpace(e.Benutzer) && passwort is not null;
        return new MailZugangDto(false, Leer(e.Host), e.Port, e.Ssl, Leer(e.Benutzer), passwort is not null, konfiguriert);
    }

    /// <summary>Ist noch kein Passwort gespeichert, muss <see cref="MailZugangSpeichernDto.Passwort"/> gesetzt sein.</summary>
    public async Task<bool> PasswortGespeichertAsync(IEinstellungen einstellungen, CancellationToken ct) =>
        Entschluesseln((await einstellungen.LesenAsync<ImapEinstellungen>(ct)).PasswortVerschluesselt) is not null;

    /// <summary>Speichert den Zugang; ein leeres Passwort behält das bisherige.</summary>
    public async Task SpeichernAsync(IEinstellungen einstellungen, MailZugangSpeichernDto neu, CancellationToken ct)
    {
        if (AusUmgebung)
        {
            throw new InvalidOperationException("Der IMAP-Zugang ist per .env gesetzt.");
        }

        var alt = await einstellungen.LesenAsync<ImapEinstellungen>(ct);
        await einstellungen.SpeichernAsync(
            new ImapEinstellungen
            {
                Host = neu.Host.Trim(),
                Port = neu.Port,
                Ssl = neu.Ssl,
                Benutzer = neu.Benutzer.Trim(),
                PasswortVerschluesselt = string.IsNullOrEmpty(neu.Passwort) ? alt.PasswortVerschluesselt : _schutz.Protect(neu.Passwort),
            },
            ct);
    }

    private string? Entschluesseln(string? verschluesselt)
    {
        if (string.IsNullOrEmpty(verschluesselt))
        {
            return null;
        }

        try
        {
            return _schutz.Unprotect(verschluesselt);
        }
        catch (CryptographicException)
        {
            // Schlüssel aus Pfade:Daten/schluessel verloren oder getauscht – das Passwort muss neu eingegeben werden.
            log.LogWarning("Gespeichertes IMAP-Passwort lässt sich nicht entschlüsseln; bitte in den Einstellungen neu eingeben");
            return null;
        }
    }

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
