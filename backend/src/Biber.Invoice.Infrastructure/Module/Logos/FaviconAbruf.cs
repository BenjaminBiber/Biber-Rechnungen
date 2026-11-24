using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Biber.Invoice.Infrastructure.Module.Logos;

public interface IFaviconAbruf
{
    /// <summary>Lädt das Favicon der Domain als PNG; <c>null</c>, wenn keins gefunden oder der Abruf nicht erlaubt ist.</summary>
    Task<byte[]?> AbrufenAsync(string domain, CancellationToken ct);
}

/// <summary>
/// Lädt <c>https://&lt;domain&gt;/</c>, wählt das größte Icon aus <c>&lt;link rel="icon|shortcut icon|apple-touch-icon"&gt;</c>,
/// sonst <c>/favicon.ico</c>, und wandelt es nach PNG. Schutz gegen Server-Side Request Forgery: nur https, höchstens
/// 3 Weiterleitungen, Timeout 5 s, höchstens 1 MB, und die aufgelöste Adresse darf nicht privat, Loopback oder Link-Local sein
/// (geprüft beim Verbindungsaufbau, damit auch DNS-Tricks und Weiterleitungen erfasst sind).
/// </summary>
public sealed partial class FaviconAbruf : IFaviconAbruf, IDisposable
{
    public const int MaxGroesse = 1024 * 1024;
    public const int MaxWeiterleitungen = 3;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly Func<string, Uri> _basisadresse;
    private readonly ILogger _log;

    /// <param name="basisadresse">Startadresse je Domain; Tests setzen hier einen lokalen Server ein. Standard: <c>https://&lt;domain&gt;/</c>.</param>
    /// <param name="adresseErlaubt">Prüfung der aufgelösten IP-Adresse; Standard: <see cref="IstOeffentlich"/>.</param>
    public FaviconAbruf(
        ILogger<FaviconAbruf>? log = null,
        Func<string, Uri>? basisadresse = null,
        Func<IPAddress, bool>? adresseErlaubt = null)
    {
        _log = log ?? NullLogger<FaviconAbruf>.Instance;
        _basisadresse = basisadresse ?? (d => new Uri($"https://{d}/"));
        var erlaubt = adresseErlaubt ?? IstOeffentlich;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ConnectTimeout = Timeout,
            ConnectCallback = (kontext, token) => VerbindenAsync(kontext.DnsEndPoint, erlaubt, token),
        };
        _http = new HttpClient(handler) { Timeout = Timeout };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Biber-Rechnungen", "1.0"));
    }

    public async Task<byte[]?> AbrufenAsync(string domain, CancellationToken ct)
    {
        try
        {
            var start = _basisadresse(domain.Trim().TrimEnd('/'));
            var seite = await LadenAsync(start, start.Scheme, ct);
            var kandidaten = seite is null
                ? []
                : Icons(Encoding.UTF8.GetString(seite.Value.Inhalt), seite.Value.Adresse);
            kandidaten.Add(new Uri(seite?.Adresse ?? start, "/favicon.ico"));

            foreach (var kandidat in kandidaten.Distinct())
            {
                var icon = await LadenAsync(kandidat, start.Scheme, ct);
                if (icon is not null && LogoBild.AlsLogoPng(icon.Value.Inhalt) is { } png)
                {
                    return png;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or IOException
                                       && !ct.IsCancellationRequested)
        {
            _log.LogInformation("Favicon für {Domain} nicht abrufbar: {Grund}", domain, ex.Message);
        }

        return null;
    }

    /// <summary>Öffentlich erreichbare Adresse: nicht privat, nicht Loopback, nicht Link-Local, nicht unspezifiziert.</summary>
    public static bool IstOeffentlich(IPAddress adresse)
    {
        if (adresse.IsIPv4MappedToIPv6)
        {
            adresse = adresse.MapToIPv4();
        }

        if (IPAddress.IsLoopback(adresse) || adresse.Equals(IPAddress.Any) || adresse.Equals(IPAddress.IPv6Any)
            || adresse.Equals(IPAddress.Broadcast))
        {
            return false;
        }

        if (adresse.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = adresse.GetAddressBytes();
            return !adresse.IsIPv6LinkLocal && !adresse.IsIPv6SiteLocal && !adresse.IsIPv6Multicast
                && (b[0] & 0xFE) != 0xFC; // fc00::/7 (Unique Local)
        }

        var v4 = adresse.GetAddressBytes();
        return v4[0] switch
        {
            0 or 10 or 127 => false,
            100 when v4[1] is >= 64 and <= 127 => false, // Carrier-Grade NAT
            169 when v4[1] == 254 => false,
            172 when v4[1] is >= 16 and <= 31 => false,
            192 when v4[1] == 168 => false,
            >= 224 => false, // Multicast und reserviert
            _ => true,
        };
    }

    public void Dispose() => _http.Dispose();

    private static async ValueTask<Stream> VerbindenAsync(DnsEndPoint ziel, Func<IPAddress, bool> erlaubt, CancellationToken ct)
    {
        var adressen = IPAddress.TryParse(ziel.Host, out var ip)
            ? [ip]
            : await Dns.GetHostAddressesAsync(ziel.Host, ct);
        var zulaessig = adressen.Where(erlaubt).ToArray();
        if (zulaessig.Length == 0)
        {
            throw new HttpRequestException($"Die Adresse von {ziel.Host} ist nicht öffentlich erreichbar.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(zulaessig, ziel.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Lädt eine Adresse mit höchstens <see cref="MaxWeiterleitungen"/> Weiterleitungen und <see cref="MaxGroesse"/> Bytes.</summary>
    private async Task<(byte[] Inhalt, Uri Adresse)?> LadenAsync(Uri adresse, string schema, CancellationToken ct)
    {
        for (var weiterleitung = 0; weiterleitung <= MaxWeiterleitungen; weiterleitung++)
        {
            if (adresse.Scheme != schema)
            {
                return null;
            }

            using var antwort = await _http.GetAsync(adresse, HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)antwort.StatusCode is >= 300 and < 400 && antwort.Headers.Location is { } ziel)
            {
                adresse = new Uri(adresse, ziel);
                continue;
            }

            if (!antwort.IsSuccessStatusCode || antwort.Content.Headers.ContentLength > MaxGroesse)
            {
                return null;
            }

            await using var strom = await antwort.Content.ReadAsStreamAsync(ct);
            using var puffer = new MemoryStream();
            var block = new byte[81920];
            int gelesen;
            while ((gelesen = await strom.ReadAsync(block, ct)) > 0)
            {
                if (puffer.Length + gelesen > MaxGroesse)
                {
                    return null;
                }

                puffer.Write(block, 0, gelesen);
            }

            return (puffer.ToArray(), adresse);
        }

        return null;
    }

    /// <summary>Icon-Adressen aus dem HTML, größtes zuerst.</summary>
    private static List<Uri> Icons(string html, Uri basis)
    {
        var icons = new List<(Uri Adresse, int Groesse)>();
        foreach (Match link in LinkTag().Matches(html))
        {
            var attribute = Attribut().Matches(link.Value)
                .GroupBy(a => a.Groups["name"].Value.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => WebUtility.HtmlDecode(g.First().Groups["wert"].Value));
            if (!attribute.TryGetValue("rel", out var rel) || !attribute.TryGetValue("href", out var href))
            {
                continue;
            }

            var rels = rel.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var touch = rels.Contains("apple-touch-icon") || rels.Contains("apple-touch-icon-precomposed");
            if (!touch && !rels.Contains("icon"))
            {
                continue;
            }

            if (!Uri.TryCreate(basis, href.Trim(), out var adresse))
            {
                continue;
            }

            var groesse = attribute.TryGetValue("sizes", out var sizes) ? Groesse(sizes) : 0;
            icons.Add((adresse, groesse > 0 ? groesse : touch ? 180 : 16));
        }

        return [.. icons.OrderByDescending(i => i.Groesse).Select(i => i.Adresse)];
    }

    private static int Groesse(string sizes) =>
        sizes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Split('x', 'X'))
            .Select(t => t.Length == 2 && int.TryParse(t[0], NumberStyles.None, CultureInfo.InvariantCulture, out var b) ? b : 0)
            .DefaultIfEmpty(0)
            .Max();

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();

    [GeneratedRegex("""(?<name>[a-zA-Z-]+)\s*=\s*(?:"(?<wert>[^"]*)"|'(?<wert>[^']*)'|(?<wert>[^\s>]+))""")]
    private static partial Regex Attribut();
}
