using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Erzeugt In-App-Benachrichtigungen (Glocke).</summary>
public interface IBenachrichtigungsDienst
{
    Task MeldenAsync(NeueBenachrichtigung meldung, CancellationToken ct);
}

/// <param name="NurAdmins">Nur für Benutzer mit Rolle Admin sichtbar.</param>
/// <param name="EntdoppelungsSchluessel">Gibt es bereits eine ungelesene Meldung mit diesem Schlüssel, entsteht keine neue.</param>
public sealed record NeueBenachrichtigung(
    BenachrichtigungArt Art,
    Schweregrad Schweregrad,
    string Titel,
    string Text,
    string? Link = null,
    bool NurAdmins = false,
    string? EntdoppelungsSchluessel = null);
