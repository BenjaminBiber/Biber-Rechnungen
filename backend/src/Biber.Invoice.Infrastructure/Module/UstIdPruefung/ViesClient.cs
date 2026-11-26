using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.UstIdPruefung;

/// <summary>Antwort von VIES; <see cref="Name"/> und <see cref="Anschrift"/> fehlen, wenn der Staat sie nicht herausgibt.</summary>
public sealed record ViesErgebnis(UstIdStatus Status, string? Name = null, string? Anschrift = null, string? Fehler = null);

/// <summary>Fragt VIES (Mehrwertsteuer-Informationsaustauschsystem der EU-Kommission) nach einer USt-IdNr.</summary>
public interface IViesClient
{
    /// <param name="land">Länderpräfix wie in VIES, z. B. „DE“ oder „EL“.</param>
    /// <param name="nummer">Nummer ohne Länderpräfix.</param>
    Task<ViesErgebnis> PruefenAsync(string land, string nummer, CancellationToken ct);
}

/// <summary>
/// REST-Schnittstelle von VIES: <c>GET ms/{land}/vat/{nummer}</c>. <c>userError</c> unterscheidet „gültig“/„ungültig“
/// von Störungen beim Mitgliedstaat (<c>MS_UNAVAILABLE</c>, <c>TIMEOUT</c> …), bei denen <c>isValid</c> ebenfalls <c>false</c> ist.
/// </summary>
public sealed class ViesClient(HttpClient http) : IViesClient
{
    public async Task<ViesErgebnis> PruefenAsync(string land, string nummer, CancellationToken ct)
    {
        ViesAntwort? antwort;
        try
        {
            using var r = await http.GetAsync($"ms/{Uri.EscapeDataString(land)}/vat/{Uri.EscapeDataString(nummer)}", ct);
            if (!r.IsSuccessStatusCode)
            {
                return new ViesErgebnis(UstIdStatus.NichtErreichbar, Fehler: $"VIES antwortet mit Status {(int)r.StatusCode}");
            }

            antwort = await r.Content.ReadFromJsonAsync<ViesAntwort>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                                   && !ct.IsCancellationRequested)
        {
            return new ViesErgebnis(UstIdStatus.NichtErreichbar, Fehler: ex.Message);
        }

        return Auswerten(antwort);
    }

    internal static ViesErgebnis Auswerten(ViesAntwort? antwort)
    {
        if (antwort is null)
        {
            return new ViesErgebnis(UstIdStatus.NichtErreichbar, Fehler: "Leere Antwort von VIES");
        }

        var fehler = antwort.UserError?.Trim().ToUpperInvariant();
        var status = fehler switch
        {
            "VALID" => UstIdStatus.Gueltig,
            "INVALID" or "INVALID_INPUT" => UstIdStatus.Ungueltig,
            null or "" => antwort.IsValid ? UstIdStatus.Gueltig : UstIdStatus.Ungueltig,
            _ => UstIdStatus.NichtErreichbar,
        };

        return status == UstIdStatus.NichtErreichbar
            ? new ViesErgebnis(status, Fehler: fehler)
            : new ViesErgebnis(status, Angabe(antwort.Name), Angabe(antwort.Address));
    }

    /// <summary>VIES schreibt „---“, wenn der Staat die Angabe nicht herausgibt.</summary>
    private static string? Angabe(string? text)
    {
        var t = text?.Trim();
        return string.IsNullOrEmpty(t) || t.All(c => c == '-') ? null : t.ReplaceLineEndings(", ");
    }

    internal sealed record ViesAntwort(
        [property: JsonPropertyName("isValid")] bool IsValid,
        [property: JsonPropertyName("userError")] string? UserError,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("address")] string? Address);
}
