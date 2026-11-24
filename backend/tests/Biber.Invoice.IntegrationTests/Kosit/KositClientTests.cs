using System.Text;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Kosit;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Biber.Invoice.IntegrationTests.Kosit;

public sealed class KositClientTests : IDisposable
{
    private static readonly byte[] Xml = Encoding.UTF8.GetBytes("<Invoice/>");

    private const string AbgelehnterBericht = """
        <rep:report xmlns:rep="http://www.xoev.de/de/validator/varl/1" valid="false">
          <rep:scenarioMatched>
            <rep:validationStepResult id="val-sch.1" valid="false">
              <rep:message id="val-sch.1.1" level="error" code="BR-DE-15">Käuferreferenz fehlt</rep:message>
              <rep:message id="val-sch.1.2" level="warning" code="BR-DE-21">Nur ein Hinweis</rep:message>
            </rep:validationStepResult>
          </rep:scenarioMatched>
        </rep:report>
        """;

    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    private KositClient Client(TimeSpan? timeout = null) =>
        new(new HttpClient { BaseAddress = new Uri(_server.Url! + "/"), Timeout = timeout ?? TimeSpan.FromSeconds(60) });

    [Fact]
    public async Task Status_200_mit_leerem_Bericht_ist_gueltig()
    {
        _server.Given(Request.Create().WithPath("/validate").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithBody("""<rep:report xmlns:rep="http://www.xoev.de/de/validator/varl/1" valid="true"/>"""));

        var ergebnis = await Client().ValidierenAsync(Xml, CancellationToken.None);

        ergebnis.Gueltig.ShouldBeTrue();
        ergebnis.Meldungen.ShouldBeEmpty();
    }

    [Fact]
    public async Task Status_406_ist_ungueltig_und_nennt_nur_Fehler_mit_Regel_ID()
    {
        _server.Given(Request.Create().WithPath("/validate").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(406).WithBody(AbgelehnterBericht));

        var ergebnis = await Client().ValidierenAsync(Xml, CancellationToken.None);

        ergebnis.Gueltig.ShouldBeFalse();
        ergebnis.Meldungen.ShouldBe(["BR-DE-15 Käuferreferenz fehlt"]);
    }

    [Fact]
    public async Task Status_503_wirft_nicht_erreichbar()
    {
        _server.Given(Request.Create().UsingPost()).RespondWith(Response.Create().WithStatusCode(503));

        await Should.ThrowAsync<KositNichtErreichbarException>(() => Client().ValidierenAsync(Xml, CancellationToken.None));
        await Should.ThrowAsync<KositNichtErreichbarException>(() => Client().VisualisierenAsync(Xml, CancellationToken.None));
    }

    [Fact]
    public async Task Timeout_wirft_nicht_erreichbar()
    {
        _server.Given(Request.Create().WithPath("/validate").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithDelay(TimeSpan.FromSeconds(2)));

        await Should.ThrowAsync<KositNichtErreichbarException>(
            () => Client(TimeSpan.FromMilliseconds(200)).ValidierenAsync(Xml, CancellationToken.None));
    }

    [Fact]
    public async Task Verbindungsfehler_wirft_nicht_erreichbar()
    {
        var client = new KositClient(new HttpClient { BaseAddress = new Uri("http://127.0.0.1:1/") });

        await Should.ThrowAsync<KositNichtErreichbarException>(() => client.ValidierenAsync(Xml, CancellationToken.None));
    }

    [Fact]
    public async Task Visualisierung_liefert_HTML()
    {
        _server.Given(Request.Create().WithPath("/visualize").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><body>RE-4711</body></html>"));

        var html = await Client().VisualisierenAsync(Xml, CancellationToken.None);

        html.ShouldContain("RE-4711");
        _server.LogEntries.Single().RequestMessage!.Headers!["Content-Type"].Single().ShouldStartWith("application/xml");
    }
}
