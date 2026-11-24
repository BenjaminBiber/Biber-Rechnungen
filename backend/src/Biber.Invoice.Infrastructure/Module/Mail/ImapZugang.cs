namespace Biber.Invoice.Infrastructure.Module.Mail;

/// <summary>Zugang zum Postfach aus der Konfiguration (<c>Imap:*</c>, gesetzt über <c>.env</c>).</summary>
public sealed class ImapZugang
{
    public string? Host { get; set; }

    public int Port { get; set; } = 993;

    public string? Benutzer { get; set; }

    /// <summary>Wird nie protokolliert oder ausgeliefert.</summary>
    public string? Passwort { get; set; }

    /// <summary><c>true</c> = TLS ab Verbindungsaufbau, sonst STARTTLS, wenn der Server es anbietet.</summary>
    public bool Ssl { get; set; } = true;

    /// <summary>Host, Benutzer und Passwort sind gesetzt.</summary>
    public bool Konfiguriert =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Benutzer) && !string.IsNullOrWhiteSpace(Passwort);
}
