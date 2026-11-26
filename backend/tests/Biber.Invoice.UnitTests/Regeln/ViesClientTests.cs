using System.Net;
using System.Text;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.UstIdPruefung;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class ViesClientTests
{
    [Theory]
    [InlineData("DE 284 117 560", "DE", "284117560")]
    [InlineData("atu12345678", "AT", "U12345678")]
    [InlineData("GR123456789", "EL", "123456789")]
    [InlineData("EL123456789", "EL", "123456789")]
    public void Zerlegt_EU_Kennungen(string ustIdNr, string land, string nummer)
    {
        UstIdZerlegung.Zerlegen(ustIdNr).ShouldBe((land, nummer));
    }

    [Theory]
    [InlineData("CHE-123.456.789")]
    [InlineData("GB123456789")]
    [InlineData("DE")]
    [InlineData(null)]
    public void Fremde_oder_kaputte_Kennungen_sind_nicht_pruefbar(string? ustIdNr)
    {
        UstIdZerlegung.Zerlegen(ustIdNr).ShouldBeNull();
    }

    [Fact]
    public async Task Gueltig_mit_Name_und_Anschrift()
    {
        var (client, anfragen) = Client(HttpStatusCode.OK, """
            {"isValid":true,"userError":"VALID","name":"ACME SRL","address":"VIA ROMA 1\n00100 ROMA"}
            """);

        var ergebnis = await client.PruefenAsync("IT", "12345678901", CancellationToken.None);

        ergebnis.ShouldBe(new ViesErgebnis(UstIdStatus.Gueltig, "ACME SRL", "VIA ROMA 1, 00100 ROMA"));
        anfragen.ShouldBe(["https://vies.test/ms/IT/vat/12345678901"]);
    }

    [Fact]
    public async Task Deutschland_gibt_keinen_Namen_heraus()
    {
        var (client, _) = Client(HttpStatusCode.OK, """{"isValid":true,"userError":"VALID","name":"---","address":"---"}""");

        (await client.PruefenAsync("DE", "284117560", CancellationToken.None))
            .ShouldBe(new ViesErgebnis(UstIdStatus.Gueltig));
    }

    [Fact]
    public async Task Ungueltig()
    {
        var (client, _) = Client(HttpStatusCode.OK, """{"isValid":false,"userError":"INVALID","name":"---","address":"---"}""");

        (await client.PruefenAsync("DE", "111111111", CancellationToken.None)).Status.ShouldBe(UstIdStatus.Ungueltig);
    }

    [Fact]
    public async Task Stoerung_beim_Mitgliedstaat_ist_nicht_ungueltig()
    {
        var (client, _) = Client(HttpStatusCode.OK, """{"isValid":false,"userError":"MS_UNAVAILABLE"}""");

        var ergebnis = await client.PruefenAsync("DE", "284117560", CancellationToken.None);

        ergebnis.Status.ShouldBe(UstIdStatus.NichtErreichbar);
        ergebnis.Fehler.ShouldBe("MS_UNAVAILABLE");
    }

    [Fact]
    public async Task Http_Fehler_ist_nicht_erreichbar()
    {
        var (client, _) = Client(HttpStatusCode.ServiceUnavailable, "");

        (await client.PruefenAsync("DE", "284117560", CancellationToken.None)).Status.ShouldBe(UstIdStatus.NichtErreichbar);
    }

    private static (ViesClient Client, List<string> Anfragen) Client(HttpStatusCode status, string json)
    {
        var anfragen = new List<string>();
        var http = new HttpClient(new Antwort(status, json, anfragen)) { BaseAddress = new Uri("https://vies.test/") };
        return (new ViesClient(http), anfragen);
    }

    private sealed class Antwort(HttpStatusCode status, string json, List<string> anfragen) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            anfragen.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
