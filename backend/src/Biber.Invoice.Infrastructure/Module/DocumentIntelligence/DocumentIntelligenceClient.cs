using System.Globalization;
using System.Text.RegularExpressions;
using Azure;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AzureDi = Azure.AI.DocumentIntelligence;

namespace Biber.Invoice.Infrastructure.Module.DocumentIntelligence;

/// <summary>
/// Azure Document Intelligence mit Modell <c>prebuilt-invoice</c>. Wiederholungen übernimmt die Pipeline
/// (Backoff im Orchestrator); der Client selbst wiederholt nicht, sondern meldet vorübergehende Fehler als
/// <see cref="DiVoruebergehendNichtVerfuegbarException"/>.
/// </summary>
public sealed partial class DocumentIntelligenceClient : IDocumentIntelligenceClient
{
    public const string Modell = "prebuilt-invoice";

    /// <summary>Felder, deren Konfidenz gemittelt wird; fehlende Felder zählen als 0.</summary>
    private static readonly string[] KonfidenzFelder = ["InvoiceId", "InvoiceDate", "InvoiceTotal", "VendorName"];

    private readonly AzureDi.DocumentIntelligenceClient? _client;
    private readonly AzureDi.DocumentIntelligenceAdministrationClient? _verwaltung;
    private readonly TimeSpan _timeout;
    private readonly ILogger<DocumentIntelligenceClient> _log;

    public DocumentIntelligenceClient(IOptions<DocumentIntelligenceOptionen> optionen, ILogger<DocumentIntelligenceClient> log)
    {
        _log = log;
        var o = optionen.Value;
        _timeout = TimeSpan.FromSeconds(Math.Max(o.TimeoutSekunden, 1));
        if (string.IsNullOrWhiteSpace(o.Endpunkt) || string.IsNullOrWhiteSpace(o.Schluessel))
        {
            return;
        }

        var clientOptionen = new AzureDi.DocumentIntelligenceClientOptions();
        clientOptionen.Retry.MaxRetries = 0;
        clientOptionen.Retry.NetworkTimeout = _timeout;
        var endpunkt = new Uri(o.Endpunkt);
        var schluessel = new AzureKeyCredential(o.Schluessel);
        _client = new AzureDi.DocumentIntelligenceClient(endpunkt, schluessel, clientOptionen);
        _verwaltung = new AzureDi.DocumentIntelligenceAdministrationClient(endpunkt, schluessel, clientOptionen);
    }

    public bool IstKonfiguriert => _client is not null;

    public async Task<ExtraktionsErgebnis> AnalysierenAsync(Stream datei, CancellationToken ct)
    {
        var client = _client ?? throw new InvalidOperationException("Document Intelligence ist nicht konfiguriert.");
        var inhalt = await BinaryData.FromStreamAsync(datei, ct);
        using var zeitlimit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        zeitlimit.CancelAfter(_timeout);

        AzureDi.AnalyzeResult ergebnis;
        try
        {
            var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, Modell, inhalt, zeitlimit.Token);
            ergebnis = operation.Value;
        }
        catch (Exception ex) when (IstVoruebergehend(ex, ct))
        {
            _log.LogWarning("Document Intelligence nicht erreichbar: {Fehler}", Beschreiben(ex));
            throw new DiVoruebergehendNichtVerfuegbarException($"Document Intelligence nicht erreichbar ({Beschreiben(ex)}).", ex);
        }

        return Abbilden(ergebnis);
    }

    public async Task<bool> VerbindungTestenAsync(CancellationToken ct)
    {
        if (_verwaltung is null)
        {
            return false;
        }

        using var zeitlimit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        zeitlimit.CancelAfter(_timeout);
        try
        {
            using var antwort = await _verwaltung.GetModelAsync(Modell, new RequestContext { CancellationToken = zeitlimit.Token });
            return !antwort.IsError;
        }
        catch (Exception ex) when (ex is RequestFailedException || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            _log.LogWarning("Verbindungstest zu Document Intelligence fehlgeschlagen: {Fehler}", Beschreiben(ex));
            return false;
        }
    }

    /// <summary>Bildet die Antwort von <c>prebuilt-invoice</c> auf ein <see cref="ExtraktionsErgebnis"/> ab.</summary>
    public static ExtraktionsErgebnis Abbilden(AzureDi.AnalyzeResult ergebnis)
    {
        IReadOnlyDictionary<string, AzureDi.DocumentField>? felder = ergebnis.Documents.FirstOrDefault()?.Fields;

        var konfidenz = KonfidenzFelder.Average(name => (decimal)(Feld(felder, name)?.Confidence ?? 0f));

        return new ExtraktionsErgebnis
        {
            Quelle = ExtraktionsQuelle.DocumentIntelligence,
            Belegart = Belegart.Rechnung,
            Rechnungsnummer = Text(Feld(felder, "InvoiceId")),
            Rechnungsdatum = Datum(Feld(felder, "InvoiceDate")),
            Faelligkeitsdatum = Datum(Feld(felder, "DueDate")),
            Leistungsdatum = Datum(Feld(felder, "ServiceDate")),
            LeistungVon = Datum(Feld(felder, "ServiceStartDate")),
            LeistungBis = Datum(Feld(felder, "ServiceEndDate")),
            Waehrung = Feld(felder, "InvoiceTotal")?.ValueCurrency?.CurrencyCode
                ?? Feld(felder, "SubTotal")?.ValueCurrency?.CurrencyCode
                ?? "EUR",
            Netto = Betrag(Feld(felder, "SubTotal")),
            Steuer = Betrag(Feld(felder, "TotalTax")),
            Brutto = Betrag(Feld(felder, "InvoiceTotal")),
            Steuerzeilen = Steuerzeilen(Feld(felder, "TaxDetails")),
            Positionen = Positionen(Feld(felder, "Items")),
            Aussteller = Partei(
                Text(Feld(felder, "VendorName")),
                Feld(felder, "VendorAddress"),
                Text(Feld(felder, "VendorTaxId")),
                Ibans(Feld(felder, "PaymentDetails"))),
            Empfaenger = Partei(Text(Feld(felder, "CustomerName")), Feld(felder, "CustomerAddress"), null, []),
            Volltext = ergebnis.Content,
            Konfidenz = Math.Round(konfidenz, 4),
            Seiten = ergebnis.Pages?.Count ?? 0,
        };
    }

    private static bool IstVoruebergehend(Exception ex, CancellationToken ct) => ex switch
    {
        RequestFailedException r => r.Status is 0 or 408 or 429 or >= 500,
        OperationCanceledException => !ct.IsCancellationRequested,
        HttpRequestException or TimeoutException => true,
        _ => false,
    };

    private static string Beschreiben(Exception ex) => ex switch
    {
        RequestFailedException { Status: > 0 } r => $"HTTP {r.Status}",
        OperationCanceledException => "Zeitüberschreitung",
        _ => "Netzwerkfehler",
    };

    private static AzureDi.DocumentField? Feld(IReadOnlyDictionary<string, AzureDi.DocumentField>? felder, string name) =>
        felder is not null && felder.TryGetValue(name, out var feld) ? feld : null;

    private static string? Text(AzureDi.DocumentField? feld)
    {
        var text = feld?.ValueString ?? feld?.Content;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static DateOnly? Datum(AzureDi.DocumentField? feld) =>
        feld?.ValueDate is { } datum ? DateOnly.FromDateTime(datum.DateTime) : null;

    private static decimal? Betrag(AzureDi.DocumentField? feld) =>
        feld?.ValueCurrency is { } waehrung ? Math.Round((decimal)waehrung.Amount, 2) : Zahl(feld);

    private static decimal? Zahl(AzureDi.DocumentField? feld) =>
        feld?.ValueDouble is { } zahl ? (decimal)zahl : null;

    /// <summary>Steuersatz aus Texten wie „19%“, „19 %“ oder „7,00%“; Zahlen gelten bereits als Prozent.</summary>
    private static decimal? Satz(AzureDi.DocumentField? feld)
    {
        if (Zahl(feld) is { } zahl)
        {
            return zahl;
        }

        var treffer = SatzMuster().Match(Text(feld) ?? "");
        return treffer.Success
            && decimal.TryParse(treffer.Value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var satz)
            ? satz
            : null;
    }

    private static List<ExtrahierteSteuerzeile> Steuerzeilen(AzureDi.DocumentField? feld) =>
        [.. (feld?.ValueList ?? [])
            .Select(z => z.ValueDictionary)
            .Where(z => z is not null && Satz(Feld(z, "Rate")) is not null)
            .Select(z => new ExtrahierteSteuerzeile(
                Satz(Feld(z, "Rate"))!.Value,
                Betrag(Feld(z, "NetAmount")) ?? 0m,
                Betrag(Feld(z, "Amount")) ?? 0m))];

    private static List<ExtrahiertePosition> Positionen(AzureDi.DocumentField? feld) =>
        [.. (feld?.ValueList ?? [])
            .Select((p, i) => (Felder: p.ValueDictionary, Inhalt: p.Content, Nr: i + 1))
            .Select(p => new ExtrahiertePosition(
                p.Nr,
                Text(Feld(p.Felder, "Description")) ?? p.Inhalt?.Trim() ?? "",
                Zahl(Feld(p.Felder, "Quantity")),
                Text(Feld(p.Felder, "Unit")),
                Betrag(Feld(p.Felder, "UnitPrice")),
                Betrag(Feld(p.Felder, "Amount")),
                Satz(Feld(p.Felder, "TaxRate"))))];

    private static List<string> Ibans(AzureDi.DocumentField? feld) =>
        [.. (feld?.ValueList ?? [])
            .Select(z => Text(Feld(z.ValueDictionary, "IBAN")))
            .Where(iban => iban is not null)
            .Select(iban => iban!.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant())];

    private static ExtrahiertePartei? Partei(string? name, AzureDi.DocumentField? adresse, string? steuerId, IReadOnlyList<string> ibans)
    {
        var a = adresse?.ValueAddress;
        if (name is null && a is null && steuerId is null && ibans.Count == 0)
        {
            return null;
        }

        var strasse = a?.StreetAddress
            ?? (a?.Road is { } road ? $"{road} {a.HouseNumber}".Trim() : null);

        // Beginnt die Steuer-ID mit zwei Buchstaben (Ländercode), ist es eine USt-IdNr., sonst eine Steuernummer.
        var kompakt = steuerId?.Replace(" ", "", StringComparison.Ordinal);
        var istUstIdNr = kompakt is { Length: >= 2 } && char.IsAsciiLetter(kompakt[0]) && char.IsAsciiLetter(kompakt[1]);

        return new ExtrahiertePartei(
            name,
            strasse,
            a?.PostalCode,
            a?.City,
            a?.CountryRegion,
            istUstIdNr ? kompakt!.ToUpperInvariant() : null,
            istUstIdNr ? null : steuerId,
            ibans,
            Email: null,
            Website: null);
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex SatzMuster();
}
