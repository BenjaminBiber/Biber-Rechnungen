using Microsoft.AspNetCore.Identity;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Benutzerkonto (ASP.NET Core Identity). Neue Konten warten auf Freigabe durch einen Admin.</summary>
public class Benutzer : IdentityUser<Guid>
{
    public string Anzeigename { get; set; } = "";
    public bool Freigegeben { get; set; }
    public DateTimeOffset ErstelltAm { get; set; }
    public DateTimeOffset? LetzteAktivitaetAm { get; set; }
}
