using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Biber.Invoice.IntegrationTests.Mail;

/// <summary>
/// Mailserver als Testcontainer (<c>greenmail/standalone</c>): SMTP 3025 zum Einliefern, IMAP 3143 zum Abrufen,
/// ein Postfach <see cref="Adresse"/> mit Anmeldung <see cref="Benutzer"/>/<see cref="Passwort"/>.
/// </summary>
public sealed class GreenMail : IAsyncLifetime
{
    public const string Benutzer = "rechnung";
    public const string Passwort = "geheim-123";
    public const string Adresse = "rechnung@biber.test";

    private const int SmtpPort = 3025;
    private const int ImapPort = 3143;
    private const int ApiPort = 8080;

    private readonly IContainer _container = new ContainerBuilder("greenmail/standalone:2.1.3")
        .WithEnvironment(
            "GREENMAIL_OPTS",
            $"-Dgreenmail.setup.test.smtp -Dgreenmail.setup.test.imap -Dgreenmail.hostname=0.0.0.0 -Dgreenmail.users={Benutzer}:{Passwort}@biber.test")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(ImapPort, true)
        .WithPortBinding(ApiPort, true)
        .Build();

    public string Host => _container.Hostname;

    public int Imap => _container.GetMappedPublicPort(ImapPort);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await BereitAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public async Task<ImapClient> ImapAsync()
    {
        var client = new ImapClient();
        await client.ConnectAsync(Host, Imap, SecureSocketOptions.None);
        await client.AuthenticateAsync(Benutzer, Passwort);
        return client;
    }

    public async Task SendenAsync(MimeMessage nachricht)
    {
        nachricht.To.Add(MailboxAddress.Parse(Adresse));
        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(Host, _container.GetMappedPublicPort(SmtpPort), SecureSocketOptions.None);
        await smtp.SendAsync(nachricht);
        await smtp.DisconnectAsync(true);
    }

    /// <summary>Anzahl der Mails im Ordner; <c>null</c>, wenn es ihn nicht gibt.</summary>
    public async Task<int?> AnzahlAsync(string ordner)
    {
        using var client = await ImapAsync();
        IMailFolder folder;
        try
        {
            folder = ordner == "INBOX" ? client.Inbox : await client.GetFolderAsync(ordner);
        }
        catch (FolderNotFoundException)
        {
            return null;
        }

        await folder.OpenAsync(FolderAccess.ReadOnly);
        return folder.Count;
    }

    /// <summary>Kopiert alle Mails aus <paramref name="ordner"/> zurück in den Eingang.</summary>
    public async Task ZurueckInDenEingangAsync(string ordner)
    {
        using var client = await ImapAsync();
        var quelle = await client.GetFolderAsync(ordner);
        await quelle.OpenAsync(FolderAccess.ReadOnly);
        await quelle.CopyToAsync(await quelle.SearchAsync(MailKit.Search.SearchQuery.All), client.Inbox);
    }

    /// <summary>Startet GreenMail über die REST-Schnittstelle neu: alle Mails und Ordner weg, das Postfach bleibt.</summary>
    public async Task ZuruecksetzenAsync()
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://{Host}:{_container.GetMappedPublicPort(ApiPort)}") };
        try
        {
            (await http.PostAsync("/api/service/reset", null)).EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.InnerException is HttpIOException)
        {
            // GreenMail startet beim Reset neu und schließt die Verbindung dabei teils vor der Antwort.
        }
        await BereitAsync();
    }

    private async Task BereitAsync()
    {
        // Bereit ist der Server erst, wenn die Anmeldung gelingt.
        for (var versuch = 0; ; versuch++)
        {
            try
            {
                using var client = await ImapAsync();
                return;
            }
            catch (Exception) when (versuch < 120)
            {
                await Task.Delay(250);
            }
        }
    }
}
