using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Biber.Invoice.Infrastructure.Module.Banken;

/// <summary>Holt die aktuelle Bankleitzahlendatei (TXT) von der Downloadseite der Deutschen Bundesbank.</summary>
public interface IBundesbankClient
{
    /// <summary>
    /// Adresse der aktuell gültigen Datei laut Downloadseite. Sie enthält eine Prüfsumme des Inhalts und ändert sich
    /// daher nur mit einer neuen Datei (vierteljährlich).
    /// </summary>
    Task<Uri> DateiAdresseAsync(CancellationToken ct);

    Task<byte[]> LadenAsync(Uri adresse, CancellationToken ct);
}

public sealed partial class BundesbankClient : IBundesbankClient
{
    public const string StandardBasisUrl = "https://www.bundesbank.de/";

    /// <summary>Downloadseite „Bankleitzahlendateien“ relativ zu <see cref="StandardBasisUrl"/>.</summary>
    public const string DownloadSeite =
        "de/aufgaben/unbarer-zahlungsverkehr/serviceangebot/bankleitzahlen/download-bankleitzahlen-602592";

    /// <summary>Die Datei ist gut 2 MB groß; mehr als das Zehnfache ist sicher keine.</summary>
    public const long MaxGroesse = 20 * 1024 * 1024;

    private readonly HttpClient _http;

    public BundesbankClient(HttpClient http)
    {
        _http = http;
        _http.MaxResponseContentBufferSize = MaxGroesse;
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Biber-Rechnungen", "1.0"));
    }

    public async Task<Uri> DateiAdresseAsync(CancellationToken ct)
    {
        var seite = await AbfragenAsync(() => _http.GetStringAsync(DownloadSeite, ct), "Downloadseite");
        var treffer = TxtDateiLink().Match(seite);
        if (!treffer.Success)
        {
            throw new BankleitzahlenAusnahme("Auf der Downloadseite der Bundesbank ist keine Bankleitzahlendatei (TXT) verlinkt.");
        }

        return new Uri(_http.BaseAddress!, System.Net.WebUtility.HtmlDecode(treffer.Groups["pfad"].Value));
    }

    public Task<byte[]> LadenAsync(Uri adresse, CancellationToken ct) =>
        AbfragenAsync(() => _http.GetByteArrayAsync(adresse, ct), "Bankleitzahlendatei");

    /// <summary>Netzwerkfehler und Zeitüberschreitung als <see cref="BankleitzahlenAusnahme"/> mit lesbarer Meldung.</summary>
    private static async Task<T> AbfragenAsync<T>(Func<Task<T>> abfrage, string was)
    {
        try
        {
            return await abfrage();
        }
        catch (HttpRequestException ex)
        {
            var status = ex.StatusCode is { } code ? $" (HTTP {(int)code})" : "";
            throw new BankleitzahlenAusnahme($"Bundesbank nicht erreichbar – {was} konnte nicht geladen werden{status}.", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            throw new BankleitzahlenAusnahme($"Bundesbank antwortet nicht – {was} konnte nicht geladen werden.", ex);
        }
    }

    [GeneratedRegex(@"href=""(?<pfad>[^""]*/blz-aktuell-txt-data\.txt)""", RegexOptions.IgnoreCase)]
    private static partial Regex TxtDateiLink();
}
