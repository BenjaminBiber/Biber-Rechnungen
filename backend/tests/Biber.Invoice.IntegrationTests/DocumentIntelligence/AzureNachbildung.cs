using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Biber.Invoice.IntegrationTests.DocumentIntelligence;

/// <summary>Nachbildung der Azure-Document-Intelligence-API mit WireMock.</summary>
public static class AzureNachbildung
{
    public const string AnalysePfad = "/documentintelligence/documentModels/prebuilt-invoice:analyze";
    public const string ErgebnisPfad = "/documentintelligence/documentModels/prebuilt-invoice/analyzeResults/op-1";
    public const string ModellPfad = "/documentintelligence/documentModels/prebuilt-invoice";

    /// <summary>Antwort von <c>prebuilt-invoice</c> aus <c>Testdateien/di/rechnung.json</c>.</summary>
    public static string Antwort => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdateien", "di", "rechnung.json"));

    /// <summary>Richtet POST (202 mit <c>Operation-Location</c>) und GET (Ergebnis) wie bei Azure ein.</summary>
    public static void AnalyseNachbilden(WireMockServer azure, string ergebnis)
    {
        azure.Given(Request.Create().WithPath(AnalysePfad).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(202)
                .WithHeader("Operation-Location", $"{azure.Url}{ErgebnisPfad}?api-version=2024-11-30")
                .WithHeader("Retry-After", "0"));
        azure.Given(Request.Create().WithPath(ErgebnisPfad).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(ergebnis));
    }

    /// <summary>Antwort auf den Verbindungstest (Abfrage des Modells).</summary>
    public static void ModellNachbilden(WireMockServer azure, int status) =>
        azure.Given(Request.Create().WithPath(ModellPfad).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(status)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"modelId":"prebuilt-invoice"}"""));
}
