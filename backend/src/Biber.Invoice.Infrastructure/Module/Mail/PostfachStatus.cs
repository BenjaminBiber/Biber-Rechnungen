using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Auswertung;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Mail;

/// <summary>Status des Postfach-Abrufs für <c>GET system/status</c>.</summary>
public sealed class PostfachStatus(MailAbruf abruf, IServiceScopeFactory scopes, TimeProvider zeit) : ISystemStatusQuelle
{
    public string Schluessel => "postfach";

    public async Task<SystemStatusEintragDto> LesenAsync(CancellationToken ct)
    {
        if (!await abruf.KonfiguriertAsync(ct))
        {
            return Eintrag(Schweregrad.Info, "E-Mail-Import nicht eingerichtet", "IMAP-Zugang fehlt – in den Einstellungen oder per .env eintragen");
        }

        using var scope = scopes.CreateScope();
        var einstellungen = await scope.ServiceProvider.GetRequiredService<IEinstellungen>().LesenAsync<MailEinstellungen>(ct);
        if (!einstellungen.Aktiv)
        {
            return Eintrag(Schweregrad.Info, "E-Mail-Import ausgeschaltet", "Abruf in den Einstellungen deaktiviert");
        }

        var status = await MailStatusSpeicher.LesenAsync(scope.ServiceProvider.GetRequiredService<BiberDbContext>(), ct);
        if (status?.LetzterAbruf is not { } letzter)
        {
            return Eintrag(Schweregrad.Info, "Postfach noch nicht abgerufen", "Erster Abruf folgt in Kürze");
        }

        if (status.Fehler is { } fehler)
        {
            return Eintrag(Schweregrad.Fehler, "Postfach-Abruf fehlgeschlagen", fehler);
        }

        var anhaenge = status.Anhaenge == 1 ? "1 neuer Anhang" : $"{status.Anhaenge} neue Anhänge";
        return Eintrag(Schweregrad.Ok, "Postfach abgerufen", $"{Vor(zeit.GetUtcNow() - letzter)} · {anhaenge}");
    }

    /// <summary>Relative Zeitangabe wie im Design, z. B. „vor 3 Min.“.</summary>
    public static string Vor(TimeSpan dauer) => dauer switch
    {
        _ when dauer < TimeSpan.FromMinutes(1) => "gerade eben",
        _ when dauer < TimeSpan.FromHours(1) => $"vor {(int)dauer.TotalMinutes} Min.",
        _ when dauer < TimeSpan.FromDays(1) => $"vor {(int)dauer.TotalHours} Std.",
        _ => (int)dauer.TotalDays == 1 ? "vor 1 Tag" : $"vor {(int)dauer.TotalDays} Tagen",
    };

    private SystemStatusEintragDto Eintrag(Schweregrad schweregrad, string titel, string text) =>
        new(Schluessel, schweregrad, titel, text, zeit.GetUtcNow());
}
