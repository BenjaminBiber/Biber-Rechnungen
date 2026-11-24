using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Auswertung;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.OrdnerWatcher;

/// <summary>Status des Eingangsordners für <c>GET system/status</c>.</summary>
public sealed class EingangsordnerStatus(OrdnerAbtaster abtaster, TimeProvider zeit) : ISystemStatusQuelle
{
    public string Schluessel => "eingangsordner";

    public Task<SystemStatusEintragDto> LesenAsync(CancellationToken ct)
    {
        try
        {
            if (Directory.Exists(abtaster.Eingang))
            {
                var wartend = OrdnerAbtaster.Kandidaten(abtaster.Eingang).Count();
                return Task.FromResult(Eintrag(Schweregrad.Ok, "Eingangsordner überwacht", $"Rechnungen-Eingang · {wartend} Dateien wartend"));
            }
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
        }

        return Task.FromResult(Eintrag(Schweregrad.Fehler, "Eingangsordner nicht erreichbar", abtaster.Eingang));
    }

    private SystemStatusEintragDto Eintrag(Schweregrad schweregrad, string titel, string text) =>
        new(Schluessel, schweregrad, titel, text, zeit.GetUtcNow());
}
