using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.DocumentIntelligence;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.Extensions.DependencyInjection;
using WireMock.Server;

namespace Biber.Invoice.IntegrationTests.DocumentIntelligence;

[Collection("Api")]
public sealed class DocumentIntelligenceEndpunktTests(ApiFabrik fabrik) : IAsyncLifetime, IDisposable
{
    private static readonly DateOnly Januar = new(2026, 1, 1);

    private readonly WireMockServer _azure = WireMockServer.Start();

    public async Task InitializeAsync()
    {
        await fabrik.ZuruecksetzenAsync();
        await fabrik.SystemTagsSicherstellenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _azure.Stop();

    [Fact]
    public async Task Verbrauch_liefert_Seiten_Limit_Prozent_und_Wartende()
    {
        var f = fabrik.MitDi(new FakeDiClient());
        await f.DiEinstellenAsync(limit: 500);
        await f.VerbrauchSetzenAsync(Januar, 412);

        var antwort = await f.ErzeugeClient(Rolle.NurLesen).GetAsync(ApiRouten.Einstellungen.DiVerbrauch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<DiVerbrauchDto>()).ShouldBe(new DiVerbrauchDto("2026-01", 412, 500, 82, false, 0));
    }

    [Fact]
    public async Task Verbrauch_braucht_eine_Anmeldung()
    {
        var antwort = await fabrik.ErzeugeAnonymenClient().GetAsync(ApiRouten.Einstellungen.DiVerbrauch);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(Rolle.Benutzer)]
    [InlineData(Rolle.NurLesen)]
    public async Task Fortsetzen_und_Verbindungstest_nur_fuer_Admin(Rolle rolle)
    {
        var client = fabrik.ErzeugeClient(rolle);

        (await client.PostAsync(ApiRouten.Einstellungen.DiFortsetzen, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync(ApiRouten.Einstellungen.DiVerbindungTesten, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Fortsetzen_setzt_pausierte_Belege_nach_Limit_Erhoehung_fort()
    {
        var di = new FakeDiClient();
        var f = fabrik.MitDi(di);
        await f.DiEinstellenAsync(limit: 10);
        await f.VerbrauchSetzenAsync(Januar, 10);
        var vorgaenge = new[]
        {
            await f.ImportiereAsync("a.pdf", DiTestHilfen.Pdf),
            await f.ImportiereAsync("b.pdf", DiTestHilfen.Pdf),
            await f.ImportiereAsync("c.pdf", DiTestHilfen.Pdf),
        };
        vorgaenge.ShouldAllBe(v => v.Status == ImportStatus.Pausiert);
        await f.DiEinstellenAsync(limit: 20);

        var antwort = await f.ErzeugeClient(Rolle.Admin).PostAsync(ApiRouten.Einstellungen.DiFortsetzen, null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<AktionErgebnisDto>()).ShouldBe(new AktionErgebnisDto(true, "3 Belege fortgesetzt"));
        foreach (var vorgang in vorgaenge)
        {
            (await f.VorgangAsync(vorgang.Id)).Status.ShouldBe(ImportStatus.Fertig);
        }

        di.Aufrufe.ShouldBe(3);
        (await f.VerbrauchAsync(Januar)).ShouldBe(13);
        (await f.VerbrauchLesenAsync()).Wartend.ShouldBe(0);
    }

    [Fact]
    public async Task Verbindungstest_gegen_die_Azure_Nachbildung_gelingt()
    {
        AzureNachbildung.ModellNachbilden(_azure, 200);
        var f = MitAzure();

        var antwort = await f.ErzeugeClient(Rolle.Admin).PostAsync(ApiRouten.Einstellungen.DiVerbindungTesten, null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<AktionErgebnisDto>()).ShouldBe(new AktionErgebnisDto(true, "Verbindung erfolgreich"));
        _azure.LogEntries.ShouldContain(l => l.RequestMessage!.Path == AzureNachbildung.ModellPfad);
    }

    [Fact]
    public async Task Verbindungstest_meldet_falschen_Schluessel()
    {
        AzureNachbildung.ModellNachbilden(_azure, 401);
        var f = MitAzure();

        var antwort = await f.ErzeugeClient(Rolle.Admin).PostAsync(ApiRouten.Einstellungen.DiVerbindungTesten, null);

        (await antwort.Content.LesenAsync<AktionErgebnisDto>()).ShouldBe(new AktionErgebnisDto(false, "Verbindung fehlgeschlagen"));
    }

    [Fact]
    public async Task Verbindungstest_ohne_Konfiguration_meldet_das()
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.Admin).PostAsync(ApiRouten.Einstellungen.DiVerbindungTesten, null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ergebnis = (await antwort.Content.LesenAsync<AktionErgebnisDto>()).ShouldNotBeNull();
        ergebnis.Erfolg.ShouldBeFalse();
        ergebnis.Meldung.ShouldStartWith("Document Intelligence ist nicht konfiguriert");
    }

    /// <summary>Fabrik, deren echter Client gegen die WireMock-Nachbildung spricht.</summary>
    private ApiFabrik MitAzure() =>
        fabrik.MitDiensten(d => d.PostConfigure<DocumentIntelligenceOptionen>(o =>
        {
            o.Endpunkt = _azure.Url;
            o.Schluessel = "test-schluessel";
        }));
}
