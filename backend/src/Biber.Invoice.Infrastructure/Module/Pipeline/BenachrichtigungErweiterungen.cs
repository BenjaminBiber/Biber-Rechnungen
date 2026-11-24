using Biber.Invoice.Contracts.Dienste;

namespace Biber.Invoice.Infrastructure.Module.Pipeline;

internal static class BenachrichtigungErweiterungen
{
    /// <summary>Meldet an alle registrierten Dienste (leer, solange kein Benachrichtigungsmodul registriert ist).</summary>
    public static async Task MeldenAsync(this IEnumerable<IBenachrichtigungsDienst> dienste, NeueBenachrichtigung meldung, CancellationToken ct)
    {
        foreach (var dienst in dienste)
        {
            await dienst.MeldenAsync(meldung, ct);
        }
    }
}
