using System.Globalization;
using System.Text.RegularExpressions;
using Azure;
using Biber.Invoice.Domain.Entitaeten;
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
            Positionen = Positionen(Feld(felder, "Items"), ergebnis.Content ?? ""),
            Aussteller = Partei(
                Text(Feld(felder, "VendorName")),
                Feld(felder, "VendorAddress"),
                Text(Feld(felder, "VendorTaxId")),
                Bankverbindungen(Feld(felder, "PaymentDetails"))),
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

    private static List<ExtrahiertePosition> Positionen(AzureDi.DocumentField? feld, string volltext)
    {
        var positionen = new List<ExtrahiertePosition>();
        var vorigesEnde = 0;
        foreach (var item in feld?.ValueList ?? [])
        {
            var beginn = item.Spans?.Count > 0 ? Math.Min(item.Spans[0].Offset, volltext.Length) : -1;
            var bezeichnung = Text(Feld(item.ValueDictionary, "Description"))
                ?? (beginn > vorigesEnde ? BezeichnungVorItem(volltext[vorigesEnde..beginn]) : null);
            positionen.AddRange(Aufteilen(item) ?? [Position(item, bezeichnung)]);
            if (item.Spans?.Count > 0)
            {
                vorigesEnde = Math.Min(item.Spans[^1].Offset + item.Spans[^1].Length, volltext.Length);
            }
        }

        return [.. positionen
            .Select(OhneFuellzeilen)
            .OfType<ExtrahiertePosition>()
            .Select(MengeAusText)
            .Select((p, i) => p with { Nr = i + 1 })];
    }

    /// <summary>
    /// VetStar setzt vor jede Datumszeile einen winzigen, unsichtbaren Strich (1 pt); DI liest ihn als eigene
    /// Tabellenzeile und liefert dann mitunter ein Item „-“ ohne Betrag, manchmal mit dem Datum darunter. Strich- und
    /// Datumszeilen fallen aus dem Text; bleibt nichts übrig und hat die Position keinen Preis, entfällt sie.
    /// </summary>
    private static ExtrahiertePosition? OhneFuellzeilen(ExtrahiertePosition p)
    {
        var zeilen = p.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var rest = zeilen.Where(z => !IstFuellzeile(z)).ToList();
        if (rest.Count == 0)
        {
            return p.Gesamt is null && p.Einzelpreis is null ? null : p;
        }

        return rest.Count == zeilen.Length ? p : p with { Text = string.Join('\n', rest) };
    }

    private static bool IstFuellzeile(string zeile) => DatumszeileMuster().IsMatch(zeile) || StrichzeileMuster().IsMatch(zeile);

    /// <summary>
    /// Erstreckt sich eine Position über einen Seitenumbruch, erkennt DI oft nur die Betragszeile auf der Folgeseite
    /// als Item – ohne <c>Description</c>; der Inhalt ist dann nur „7 ST 37,00 1 ST 259,00 19“. Die Bezeichnung
    /// steht davor im Volltext im Positionskopf „Pos.-Nr. / Artikelnummer / Bezeichnung“ (z. B. „0010 / 0011977388 /
    /// AXION 870 …“); gesucht wird der letzte solche Kopf zwischen vorigem Item und diesem.
    /// </summary>
    private static string? BezeichnungVorItem(string abschnitt)
    {
        var treffer = PositionskopfMuster().Matches(abschnitt);
        return treffer.Count == 0 ? null : treffer[^1].Groups["bezeichnung"].Value.Trim();
    }

    private static ExtrahiertePosition Position(AzureDi.DocumentField p, string? bezeichnung)
    {
        var felder = p.ValueDictionary;
        return new ExtrahiertePosition(
            0,
            bezeichnung ?? p.Content?.Trim() ?? "",
            Zahl(Feld(felder, "Quantity")),
            Text(Feld(felder, "Unit")),
            Betrag(Feld(felder, "UnitPrice")),
            Betrag(Feld(felder, "Amount")),
            Satz(Feld(felder, "TaxRate")));
    }

    /// <summary>
    /// Document Intelligence fasst manchmal mehrere Tabellenzeilen zu einem Item zusammen (gesehen bei
    /// Fortsetzungstabellen auf Folgeseiten); der Inhalt enthält dann mehrere Betragszeilen. Jede Betragszeile
    /// schließt eine Position ab, Datums- und Strichzeilen fallen weg. Liefert <c>null</c>, wenn nichts zu teilen ist
    /// oder der von DI erkannte Betrag nicht zur ersten Teilposition passt.
    /// </summary>
    private static List<ExtrahiertePosition>? Aufteilen(AzureDi.DocumentField p)
    {
        var zeilen = (p.Content ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (zeilen.Count(z => BetragszeileMuster().IsMatch(z)) < 2)
        {
            return null;
        }

        var teile = new List<ExtrahiertePosition>();
        var text = new List<string>();
        foreach (var zeile in zeilen)
        {
            if (BetragszeileMuster().IsMatch(zeile))
            {
                if (text.Count == 0)
                {
                    return null; // Betrag ohne Beschreibung – Struktur unklar, lieber DI übernehmen
                }

                teile.Add(new ExtrahiertePosition(0, string.Join(' ', text), null, null, null, BetragAusText(zeile), null));
                text.Clear();
            }
            else if (!IstFuellzeile(zeile))
            {
                text.Add(zeile);
            }
        }

        var diBetrag = Betrag(Feld(p.ValueDictionary, "Amount"));
        return diBetrag is null || teile[0].Gesamt == diBetrag ? teile : null;
    }

    /// <summary>„3 x 1,0 Dos Engemycin“: Menge vorn im Text, wenn DI keine eigene Menge erkannt hat.</summary>
    private static ExtrahiertePosition MengeAusText(ExtrahiertePosition p)
    {
        if (p.Menge is not null)
        {
            return p;
        }

        var treffer = MengeImTextMuster().Match(p.Text);
        if (!treffer.Success || !int.TryParse(treffer.Groups["menge"].Value, CultureInfo.InvariantCulture, out var menge) || menge <= 0)
        {
            return p;
        }

        return p with
        {
            Text = treffer.Groups["text"].Value.Trim(),
            Menge = menge,
            Einzelpreis = p.Einzelpreis ?? (p.Gesamt is { } gesamt ? Math.Round(gesamt / menge, 2) : null),
        };
    }

    /// <summary>Deutscher Betrag wie „1 534,10“, „1.534,10“ oder „-38,22“.</summary>
    private static decimal? BetragAusText(string zeile) =>
        decimal.TryParse(
            zeile.Replace(" ", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal),
            NumberStyles.Number,
            Deutsch,
            out var betrag)
            ? betrag
            : null;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    [GeneratedRegex(@"^-?\d{1,3}(?:[ .]?\d{3})*,\d{2}$")]
    private static partial Regex BetragszeileMuster();

    /// <summary>Positionsnummer, Artikelnummer (mit Ziffer) und eine Bezeichnung mit Buchstaben – je eine Zeile.</summary>
    [GeneratedRegex(@"^\d{1,6}\n(?=[^\n]*\d)[0-9A-Z][0-9A-Z./-]{3,}\n(?<bezeichnung>[^\n]*\p{L}{3}[^\n]*)$", RegexOptions.Multiline)]
    private static partial Regex PositionskopfMuster();

    [GeneratedRegex(@"^\d{1,2}\.\d{1,2}\.\d{2,4}$")]
    private static partial Regex DatumszeileMuster();

    [GeneratedRegex(@"^[-–—]+$")]
    private static partial Regex StrichzeileMuster();

    [GeneratedRegex(@"^(?<menge>\d{1,4})\s*[x×X]\s+(?<text>\S.*)$", RegexOptions.Singleline)]
    private static partial Regex MengeImTextMuster();

    /// <summary>IBAN und – falls erkannt – BIC (Feld <c>SWIFT</c>) je Eintrag von <c>PaymentDetails</c>.</summary>
    private static List<Bankverbindung> Bankverbindungen(AzureDi.DocumentField? feld) =>
        [.. (feld?.ValueList ?? [])
            .Select(z => (Iban: Text(Feld(z.ValueDictionary, "IBAN")), Bic: Text(Feld(z.ValueDictionary, "SWIFT"))))
            .Where(z => z.Iban is not null)
            .Select(z => new Bankverbindung(OhneLeerzeichen(z.Iban!), z.Bic is null ? null : OhneLeerzeichen(z.Bic)))];

    private static string OhneLeerzeichen(string wert) => wert.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    private static ExtrahiertePartei? Partei(string? name, AzureDi.DocumentField? adresse, string? steuerId, IReadOnlyList<Bankverbindung> bankverbindungen)
    {
        var a = adresse?.ValueAddress;
        if (name is null && a is null && steuerId is null && bankverbindungen.Count == 0)
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
            bankverbindungen,
            Email: null,
            Website: null);
    }

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex SatzMuster();
}
