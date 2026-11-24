using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using System.Xml.Linq;
using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Infrastructure.Module.Kosit;

/// <summary>
/// HTTP-Client für den KoSIT-Sidecar (<c>Kosit:BasisUrl</c>). <c>POST /validate</c> antwortet 200 (akzeptiert)
/// oder 406 (abgelehnt) mit dem Prüfbericht <c>rep:report</c>; <c>POST /visualize</c> liefert HTML.
/// </summary>
public sealed class KositClient(HttpClient http) : IKositClient
{
    private static readonly XNamespace Rep = "http://www.xoev.de/de/validator/varl/1";

    public async Task<KositErgebnis> ValidierenAsync(byte[] xml, CancellationToken ct)
    {
        using var antwort = await SendenAsync("validate", xml, ct);
        if (antwort.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NotAcceptable))
        {
            throw new HttpRequestException($"KoSIT-Validierung: unerwarteter Status {(int)antwort.StatusCode}.");
        }

        var bericht = await antwort.Content.ReadAsStringAsync(ct);
        return new KositErgebnis(antwort.StatusCode == HttpStatusCode.OK, Meldungen(bericht));
    }

    public async Task<string> VisualisierenAsync(byte[] xml, CancellationToken ct)
    {
        using var antwort = await SendenAsync("visualize", xml, ct);
        if (!antwort.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"KoSIT-Visualisierung: unerwarteter Status {(int)antwort.StatusCode}.");
        }

        return await antwort.Content.ReadAsStringAsync(ct);
    }

    /// <summary>Je Fehlermeldung des Berichts „Regel-ID Text“; Warnungen und Hinweise entfallen.</summary>
    internal static IReadOnlyList<string> Meldungen(string bericht)
    {
        if (string.IsNullOrWhiteSpace(bericht))
        {
            return [];
        }

        XDocument dokument;
        try
        {
            dokument = XDocument.Parse(bericht);
        }
        catch (XmlException)
        {
            return [];
        }

        return dokument.Descendants(Rep + "message")
            .Where(m => string.Equals((string?)m.Attribute("level"), "error", StringComparison.OrdinalIgnoreCase))
            .Select(m =>
            {
                var regel = (string?)m.Attribute("code") ?? (string?)m.Attribute("id");
                var text = m.Value.Trim();
                return string.IsNullOrWhiteSpace(regel) ? text : $"{regel} {text}";
            })
            .Distinct()
            .ToList();
    }

    private async Task<HttpResponseMessage> SendenAsync(string pfad, byte[] xml, CancellationToken ct)
    {
        using var inhalt = new ByteArrayContent(xml);
        inhalt.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        HttpResponseMessage antwort;
        try
        {
            antwort = await http.PostAsync(pfad, inhalt, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new KositNichtErreichbarException("KoSIT-Validator nicht erreichbar.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new KositNichtErreichbarException("KoSIT-Validator antwortet nicht (Timeout).", ex);
        }

        if ((int)antwort.StatusCode >= 500)
        {
            antwort.Dispose();
            throw new KositNichtErreichbarException($"KoSIT-Validator meldet Status {(int)antwort.StatusCode}.");
        }

        return antwort;
    }
}
