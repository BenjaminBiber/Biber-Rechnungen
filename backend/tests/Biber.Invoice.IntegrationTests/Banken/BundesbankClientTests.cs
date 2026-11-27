using System.Text;
using Biber.Invoice.Infrastructure.Module.Banken;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Biber.Invoice.IntegrationTests.Banken;

/// <summary>Abruf der Bankleitzahlendatei gegen einen lokalen WireMock-Server; kein Test geht ins Internet.</summary>
public sealed class BundesbankClientTests : IDisposable
{
    private const string Blob = "/resource/blob/602632/3a97fff9893732591adbbfcb70c1c4e8/472B63F073F071307366337C94F8C870/blz-aktuell-txt-data.txt";

    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    [Fact]
    public async Task Findet_den_TXT_Link_auf_der_Downloadseite_und_laedt_die_Datei()
    {
        Seite($"""
               <a href="/resource/blob/602678/x/blz-aktuell-txt-zip-data.zip">ZIP</a>
               <a href="{Blob}" class="download">Bankleitzahlendatei TXT</a>
               <a href="/resource/blob/602702/y/blz-neu-txt-data.txt">nächste Datei</a>
               """);
        var inhalt = Encoding.Latin1.GetBytes("100000001Bundesbank");
        _server.Given(Request.Create().WithPath(Blob).UsingGet())
            .RespondWith(Response.Create().WithBody(inhalt));
        var client = Client();

        var adresse = await client.DateiAdresseAsync(default);

        adresse.ShouldBe(new Uri($"{_server.Url}{Blob}"));
        (await client.LadenAsync(adresse, default)).ShouldBe(inhalt);
    }

    [Fact]
    public async Task Seite_ohne_Link_ergibt_eine_lesbare_Meldung()
    {
        Seite("<p>Wartungsarbeiten</p>");

        var ex = await Should.ThrowAsync<BankleitzahlenAusnahme>(() => Client().DateiAdresseAsync(default));

        ex.Message.ShouldContain("keine Bankleitzahlendatei");
    }

    [Fact]
    public async Task Serverfehler_ergibt_eine_lesbare_Meldung_mit_Status()
    {
        _server.Given(Request.Create().WithPath("/" + BundesbankClient.DownloadSeite).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        var ex = await Should.ThrowAsync<BankleitzahlenAusnahme>(() => Client().DateiAdresseAsync(default));

        ex.Message.ShouldBe("Bundesbank nicht erreichbar – Downloadseite konnte nicht geladen werden (HTTP 503).");
    }

    private void Seite(string html) =>
        _server.Given(Request.Create().WithPath("/" + BundesbankClient.DownloadSeite).UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "text/html; charset=utf-8").WithBody(html));

    private BundesbankClient Client() => new(new HttpClient { BaseAddress = new Uri(_server.Url! + "/") });
}
