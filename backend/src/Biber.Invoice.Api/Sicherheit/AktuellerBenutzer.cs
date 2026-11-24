using System.Security.Claims;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Api.Sicherheit;

/// <summary>
/// Liest den Benutzer aus <see cref="HttpContext.User"/>. Ohne HTTP-Kontext (Hintergrundjobs)
/// ist es das System, das alle Rollen hat.
/// </summary>
public sealed class AktuellerBenutzer(IHttpContextAccessor httpContextAccessor) : IAktuellerBenutzer
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IstSystem => httpContextAccessor.HttpContext is null;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string Anzeigename =>
        IstSystem ? "System" : Principal?.FindFirstValue(ClaimTypes.Name) ?? "Anonym";

    public bool HatRolle(Rolle rolle) => IstSystem || Principal?.IsInRole(rolle.ToString()) == true;
}
