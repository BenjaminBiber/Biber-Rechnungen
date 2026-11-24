using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using Azure.AI.DocumentIntelligence;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.DocumentIntelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using DiClient = Biber.Invoice.Infrastructure.Module.DocumentIntelligence.DocumentIntelligenceClient;

namespace Biber.Invoice.IntegrationTests.DocumentIntelligence;

/// <summary>Der Client gegen eine Nachbildung der Azure-API (WireMock): Abbildung der Felder und Fehlerklassen.</summary>
public sealed class DocumentIntelligenceClientTests : IDisposable
{
    private const string AnalysePfad = AzureNachbildung.AnalysePfad;
    private const string ErgebnisPfad = AzureNachbildung.ErgebnisPfad;

    private readonly WireMockServer _azure = WireMockServer.Start();

    public void Dispose() => _azure.Stop();

    private static string Antwort => AzureNachbildung.Antwort;

    private static void AnalyseNachbilden(WireMockServer azure, string ergebnis) => AzureNachbildung.AnalyseNachbilden(azure, ergebnis);

    [Fact]
    public async Task Bildet_alle_Felder_der_Rechnung_ab()
    {
        AnalyseNachbilden(_azure, Antwort);

        var e = await Client().AnalysierenAsync(new MemoryStream("%PDF-1.7"u8.ToArray()), CancellationToken.None);

        e.Quelle.ShouldBe(ExtraktionsQuelle.DocumentIntelligence);
        e.Belegart.ShouldBe(Belegart.Rechnung);
        e.Rechnungsnummer.ShouldBe("RE-2026-0815");
        e.Rechnungsdatum.ShouldBe(new DateOnly(2026, 11, 3));
        e.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 11, 17));
        e.Leistungsdatum.ShouldBeNull();
        e.LeistungVon.ShouldBe(new DateOnly(2026, 10, 1));
        e.LeistungBis.ShouldBe(new DateOnly(2026, 10, 31));
        e.Waehrung.ShouldBe("EUR");
        e.Netto.ShouldBe(140.00m);
        e.Steuer.ShouldBe(26.60m);
        e.Brutto.ShouldBe(166.60m);
        e.Steuerzeilen.ShouldBe([new ExtrahierteSteuerzeile(19m, 140.00m, 26.60m)]);
        e.Positionen.ShouldBe(
        [
            new ExtrahiertePosition(1, "Toner schwarz", 2m, "Stück", 45.00m, 90.00m, 19m),
            new ExtrahiertePosition(2, "Wartung", 1m, "Pauschale", 50.00m, 50.00m, 19m),
        ]);

        var aussteller = e.Aussteller.ShouldNotBeNull();
        aussteller.Name.ShouldBe("Muster Bürotechnik GmbH");
        aussteller.Strasse.ShouldBe("Hauptstraße 12");
        aussteller.Plz.ShouldBe("10115");
        aussteller.Ort.ShouldBe("Berlin");
        aussteller.Land.ShouldBe("DEU");
        aussteller.UstIdNr.ShouldBe("DE123456789");
        aussteller.Steuernummer.ShouldBeNull();
        aussteller.Ibans.ShouldBe(["DE89370400440532013000"]);

        var empfaenger = e.Empfaenger.ShouldNotBeNull();
        empfaenger.Name.ShouldBe("Biber Beratung");
        empfaenger.Strasse.ShouldBe("Am Teich 3");
        empfaenger.Plz.ShouldBe("20095");
        empfaenger.Ort.ShouldBe("Hamburg");

        e.Volltext.ShouldNotBeNull();
        e.Volltext.ShouldStartWith("Muster Bürotechnik GmbH\nRechnung RE-2026-0815");
        e.Seiten.ShouldBe(2);
        e.Konfidenz.ShouldBe(0.85m); // (0,9 + 0,8 + 0,7 + 1,0) / 4
    }

    [Fact]
    public async Task Sendet_den_Schluessel_im_Header()
    {
        AnalyseNachbilden(_azure, Antwort);

        await Client().AnalysierenAsync(new MemoryStream([1, 2, 3]), CancellationToken.None);

        var anfrage = _azure.LogEntries.Select(l => l.RequestMessage).First(r => r?.Path == AnalysePfad);
        var header = anfrage!.Headers.ShouldNotBeNull();
        header["Ocp-Apim-Subscription-Key"].ShouldBe(["test-schluessel"]);
    }

    [Theory]
    [InlineData("DE 123456789", "DE123456789", null)]
    [InlineData("ATU12345678", "ATU12345678", null)]
    [InlineData("12/345/67890", null, "12/345/67890")]
    [InlineData("2893081508152", null, "2893081508152")]
    public void Steuer_ID_mit_Laendercode_ist_USt_IdNr_sonst_Steuernummer(string steuerId, string? ustIdNr, string? steuernummer)
    {
        var e = Abbilden(f => f["VendorTaxId"]!["valueString"] = steuerId);

        e.Aussteller!.UstIdNr.ShouldBe(ustIdNr);
        e.Aussteller.Steuernummer.ShouldBe(steuernummer);
    }

    [Fact]
    public void Fehlende_Felder_zaehlen_bei_der_Konfidenz_als_0()
    {
        var e = Abbilden(f =>
        {
            f.Remove("VendorName");
            f.Remove("InvoiceId");
        });

        e.Konfidenz.ShouldBe(0.375m); // (0 + 0,8 + 0,7 + 0) / 4
        e.Rechnungsnummer.ShouldBeNull();
        e.Aussteller!.Name.ShouldBeNull();
    }

    [Fact]
    public void Ohne_Dokument_bleibt_alles_leer_und_die_Konfidenz_ist_0()
    {
        var json = JsonNode.Parse(Antwort)!["analyzeResult"]!;
        json["documents"] = new JsonArray();

        var e = DiClient.Abbilden(ModelReaderWriter.Read<AnalyzeResult>(BinaryData.FromString(json.ToJsonString()))!);

        e.Konfidenz.ShouldBe(0m);
        e.Brutto.ShouldBeNull();
        e.Aussteller.ShouldBeNull();
        e.Seiten.ShouldBe(2);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(500)]
    [InlineData(408)]
    public async Task Ueberlastung_und_Serverfehler_sind_voruebergehend(int status)
    {
        _azure.Given(Request.Create().WithPath(AnalysePfad).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(status).WithHeader("Retry-After", "0"));

        await Should.ThrowAsync<DiVoruebergehendNichtVerfuegbarException>(
            () => Client().AnalysierenAsync(new MemoryStream([1]), CancellationToken.None));
        _azure.LogEntries.Count().ShouldBe(1); // keine eigenen Wiederholungen des SDK
    }

    [Fact]
    public async Task Serverfehler_beim_Abfragen_des_Ergebnisses_ist_voruebergehend()
    {
        _azure.Given(Request.Create().WithPath(AnalysePfad).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithHeader("Operation-Location", $"{_azure.Url}{ErgebnisPfad}?api-version=2024-11-30")
                .WithHeader("Retry-After", "0"));
        _azure.Given(Request.Create().WithPath(ErgebnisPfad).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        await Should.ThrowAsync<DiVoruebergehendNichtVerfuegbarException>(
            () => Client().AnalysierenAsync(new MemoryStream([1]), CancellationToken.None));
    }

    [Fact]
    public async Task Timeout_ist_voruebergehend()
    {
        _azure.Given(Request.Create().WithPath(AnalysePfad).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202).WithDelay(TimeSpan.FromSeconds(5)));

        await Should.ThrowAsync<DiVoruebergehendNichtVerfuegbarException>(
            () => Client(timeoutSekunden: 1).AnalysierenAsync(new MemoryStream([1]), CancellationToken.None));
    }

    [Fact]
    public async Task Nicht_erreichbarer_Endpunkt_ist_voruebergehend()
    {
        var url = _azure.Url!;
        _azure.Stop();

        await Should.ThrowAsync<DiVoruebergehendNichtVerfuegbarException>(
            () => Client(endpunkt: url).AnalysierenAsync(new MemoryStream([1]), CancellationToken.None));
    }

    [Fact]
    public async Task Falscher_Schluessel_ist_nicht_voruebergehend()
    {
        _azure.Given(Request.Create().WithPath(AnalysePfad).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(401));

        var fehler = await Should.ThrowAsync<Exception>(
            () => Client().AnalysierenAsync(new MemoryStream([1]), CancellationToken.None));
        fehler.ShouldNotBeOfType<DiVoruebergehendNichtVerfuegbarException>();
    }

    [Fact]
    public void Ohne_Endpunkt_oder_Schluessel_ist_der_Client_nicht_konfiguriert()
    {
        new DiClient(Options.Create(new DocumentIntelligenceOptionen()), NullLogger<DiClient>.Instance).IstKonfiguriert.ShouldBeFalse();
        Client().IstKonfiguriert.ShouldBeTrue();
    }

    [Fact]
    public async Task Verbindungstest_fragt_das_Modell_ab()
    {
        _azure.Given(Request.Create().WithPath("/documentintelligence/documentModels/prebuilt-invoice").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { modelId = "prebuilt-invoice" }));

        (await Client().VerbindungTestenAsync(CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task Verbindungstest_schlaegt_bei_falschem_Schluessel_fehl()
    {
        _azure.Given(Request.Create().WithPath("/documentintelligence/documentModels/prebuilt-invoice").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401));

        (await Client().VerbindungTestenAsync(CancellationToken.None)).ShouldBeFalse();
    }

    private DiClient Client(int timeoutSekunden = 30, string? endpunkt = null) =>
        new(
            Options.Create(new DocumentIntelligenceOptionen
            {
                Endpunkt = endpunkt ?? _azure.Url,
                Schluessel = "test-schluessel",
                TimeoutSekunden = timeoutSekunden,
            }),
            NullLogger<DiClient>.Instance);

    private static ExtraktionsErgebnis Abbilden(Action<JsonObject> felderAendern)
    {
        var json = JsonNode.Parse(Antwort)!["analyzeResult"]!;
        felderAendern(json["documents"]![0]!["fields"]!.AsObject());
        return DiClient.Abbilden(ModelReaderWriter.Read<AnalyzeResult>(BinaryData.FromString(json.ToJsonString()))!);
    }
}
