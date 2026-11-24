using System.Net;
using Biber.Invoice.IntegrationTests.Infrastruktur;

namespace Biber.Invoice.IntegrationTests;

[Collection("Api")]
public sealed class HealthTests(ApiFabrik fabrik)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Healthcheck_liefert_200_ohne_Anmeldung(string pfad)
    {
        var client = fabrik.ErzeugeAnonymenClient();

        var antwort = await client.GetAsync(pfad);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }
}
