using System.Net;
using Biber.Invoice.IntegrationTests.Infrastruktur;

namespace Biber.Invoice.IntegrationTests;

[Collection("Api")]
public sealed class SpaFallbackTests(ApiFabrik fabrik)
{
    private const string IndexHtml = "<!doctype html><title>Biber-Rechnungen</title>";

    [Theory]
    [InlineData("/irgendwas")]
    [InlineData("/belege/123")]
    public async Task Unbekannter_Pfad_liefert_index_html(string pfad)
    {
        await File.WriteAllTextAsync(Path.Combine(fabrik.TempOrdner, "wwwroot", "index.html"), IndexHtml);
        var client = fabrik.ErzeugeAnonymenClient();

        var antwort = await client.GetAsync(pfad);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        (await antwort.Content.ReadAsStringAsync()).ShouldBe(IndexHtml);
    }

    [Theory]
    [InlineData("/api/gibtsnicht")]
    [InlineData("/mcp/gibtsnicht")]
    [InlineData("/health/gibtsnicht")]
    public async Task Reservierte_Pfade_liefern_404(string pfad)
    {
        await File.WriteAllTextAsync(Path.Combine(fabrik.TempOrdner, "wwwroot", "index.html"), IndexHtml);
        var client = fabrik.ErzeugeAnonymenClient();

        var antwort = await client.GetAsync(pfad);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        antwort.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }
}
