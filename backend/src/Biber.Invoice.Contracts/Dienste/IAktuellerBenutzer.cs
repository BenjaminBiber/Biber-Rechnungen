using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dienste;

/// <summary>Der Benutzer, in dessen Namen die aktuelle Anfrage oder der aktuelle Job läuft.</summary>
public interface IAktuellerBenutzer
{
    /// <summary>Benutzer-ID; <c>null</c> für das System (Hintergrundjobs) oder anonyme Anfragen.</summary>
    Guid? Id { get; }

    string Anzeigename { get; }

    /// <summary><c>true</c>, wenn kein HTTP-Kontext vorliegt (z. B. Hangfire-Job, Ordner-Watcher).</summary>
    bool IstSystem { get; }

    bool HatRolle(Rolle rolle);
}
