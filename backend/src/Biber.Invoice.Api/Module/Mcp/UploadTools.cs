using System.ComponentModel;
using System.Security.Claims;
using Biber.Invoice.Api.Module.Tokens;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Enums;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Biber.Invoice.Api.Module.Mcp;

/// <summary>Tool <c>beleg_hochladen</c>: verlangt Scope „upload“ und eine Rolle mit Schreibrecht.</summary>
[McpServerToolType]
public sealed class UploadTools(IImportDienst import, IHttpContextAccessor kontext)
{
    public const long MaxGroesse = 20 * 1024 * 1024;

    [McpServerTool(Name = "beleg_hochladen")]
    [Description("Lädt eine Rechnung (PDF, XRechnung-XML oder Bild) hoch und übergibt sie der Verarbeitung. Verlangt ein Token mit Scope „upload“. Höchstens 20 MB.")]
    public async Task<string> BelegHochladenAsync(
        [Description("Dateiname mit Endung, z. B. rechnung.pdf.")] string dateiname,
        [Description("Inhalt der Datei, Base64-kodiert.")] string inhalt_base64,
        CancellationToken ct = default)
    {
        var benutzer = kontext.HttpContext?.User ?? new ClaimsPrincipal();
        if (!benutzer.HasClaim(ApiTokenHandler.ScopeClaim, TokenScopes.Upload))
        {
            throw new McpException("Scope upload fehlt.");
        }

        if (!benutzer.IsInRole(nameof(Rolle.Admin)) && !benutzer.IsInRole(nameof(Rolle.Benutzer)))
        {
            throw new McpException("Mit der Rolle „Nur lesen“ ist kein Upload erlaubt.");
        }

        if (string.IsNullOrWhiteSpace(dateiname))
        {
            throw new McpException("Dateiname fehlt.");
        }

        if ((inhalt_base64 ?? "").Length > (MaxGroesse + 2) / 3 * 4)
        {
            throw new McpException("Die Datei ist größer als 20 MB.");
        }

        byte[] inhalt;
        try
        {
            inhalt = Convert.FromBase64String(inhalt_base64 ?? "");
        }
        catch (FormatException)
        {
            throw new McpException("Der Inhalt ist kein gültiges Base64.");
        }

        if (inhalt.Length == 0)
        {
            throw new McpException("Die Datei ist leer.");
        }

        if (inhalt.Length > MaxGroesse)
        {
            throw new McpException("Die Datei ist größer als 20 MB.");
        }

        var herkunft = new ImportHerkunft(
            BenutzerId: Guid.TryParse(benutzer.FindFirstValue(ClaimTypes.NameIdentifier), out var b) ? b : null,
            ApiTokenId: Guid.TryParse(benutzer.FindFirstValue(ApiTokenHandler.TokenIdClaim), out var t) ? t : null);

        using var strom = new MemoryStream(inhalt, writable: false);
        var vorgang = await import.AnnehmenAsync(strom, dateiname, ImportQuelle.Mcp, herkunft, ct);
        return McpHilfe.Json(new { VorgangId = vorgang.Id, vorgang.Status, vorgang.Ergebnis, vorgang.Fehler });
    }
}
