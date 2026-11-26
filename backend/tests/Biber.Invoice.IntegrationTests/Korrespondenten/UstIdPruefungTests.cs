using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.UstIdPruefung;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.Korrespondenten;

[Collection("Api")]
public sealed class UstIdPruefungTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    private readonly FakeVies _vies = new();
    private ApiFabrik _mitVies = null!;

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(Jetzt);
        await fabrik.ZuruecksetzenAsync();
        foreach (var rolle in Enum.GetValues<Rolle>())
        {
            var benutzer = Testdaten.Benutzer();
            benutzer.Id = ApiFabrik.BenutzerId(rolle);
            await fabrik.SpeichernAsync(benutzer);
        }

        _mitVies = fabrik.MitDiensten(d =>
        {
            d.RemoveAll<IViesClient>();
            d.AddSingleton<IViesClient>(_vies);
        });
        _mitVies.Zeit.Setze(Jetzt);
    }

    public async Task DisposeAsync() => await _mitVies.DisposeAsync();

    [Fact]
    public async Task Jetzt_pruefen_speichert_das_Ergebnis_und_meldet_ungueltige_Nummern()
    {
        _vies.Ergebnis = new ViesErgebnis(UstIdStatus.Ungueltig);
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Fälscher GmbH", "DE111222333"));
        var beleg = await fabrik.SpeichernAsync(Testdaten.Beleg(korrespondentId: k.Id));

        var antwort = await _mitVies.ErzeugeClient(Rolle.Benutzer).PostAsync(Route(k.Id), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detail = (await antwort.Content.LesenAsync<KorrespondentDetailDto>())!;
        detail.UstIdPruefung.ShouldBe(new UstIdPruefungDto(UstIdStatus.Ungueltig, Jetzt, null, null));
        _vies.Anfragen.ShouldBe([("DE", "111222333")]);

        using var scope = _mitVies.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var meldung = await db.Benachrichtigungen.SingleAsync(m => m.Art == BenachrichtigungArt.UstIdUngueltig);
        meldung.Link.ShouldBe($"/korrespondenten/{k.Id}");
        var befund = await db.PruefBefunde.SingleAsync(f => f.BelegId == beleg.Id && f.Pruefung == PruefungArt.UstIdNr);
        befund.Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen);
    }

    [Fact]
    public async Task Job_prueft_neue_und_geaenderte_Nummern_aber_keine_frischen_Ergebnisse()
    {
        _vies.Ergebnis = new ViesErgebnis(UstIdStatus.Gueltig, "ACME SRL", "VIA ROMA 1");
        var neu = await fabrik.SpeichernAsync(Testdaten.Korrespondent(ustIdNr: "IT12345678901"));
        var frisch = Testdaten.Korrespondent(ustIdNr: "DE284117560");
        frisch.UstIdStatus = UstIdStatus.Gueltig;
        frisch.UstIdGeprueftNummer = "DE284117560";
        frisch.UstIdGeprueftAm = Jetzt.AddDays(-10);
        await fabrik.SpeichernAsync(frisch);
        var schweiz = await fabrik.SpeichernAsync(Testdaten.Korrespondent(ustIdNr: "CHE123456789"));

        await JobAsync();

        _vies.Anfragen.ShouldBe([("IT", "12345678901")]);
        using var scope = _mitVies.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var geprueft = await db.Korrespondenten.SingleAsync(x => x.Id == neu.Id);
        geprueft.UstIdStatus.ShouldBe(UstIdStatus.Gueltig);
        geprueft.UstIdAmtlicherName.ShouldBe("ACME SRL");
        (await db.Korrespondenten.SingleAsync(x => x.Id == schweiz.Id)).UstIdStatus.ShouldBe(UstIdStatus.NichtPruefbar);
    }

    [Fact]
    public async Task Abgeschaltet_prueft_der_Job_nicht()
    {
        await fabrik.SpeichernAsync(Testdaten.Korrespondent(ustIdNr: "DE284117560"));
        var antwort = await fabrik.ErzeugeClient(Rolle.Admin).PutJsonAsync(
            ApiRouten.Einstellungen.Gruppe.Replace("{gruppe}", "pruefungen", StringComparison.Ordinal),
            new Contracts.Dto.Einstellungen.PruefEinstellungen { UstIdOnlinePruefen = false });
        antwort.IsSuccessStatusCode.ShouldBeTrue();

        await JobAsync();

        _vies.Anfragen.ShouldBeEmpty();
    }

    private async Task JobAsync()
    {
        using var scope = _mitVies.ErzeugeScope();
        var job = scope.ServiceProvider.GetServices<IWiederkehrenderJob>().OfType<UstIdPruefJob>().Single();
        await job.AusfuehrenAsync(CancellationToken.None);
    }

    private static string Route(Guid id) => ApiRouten.Korrespondenten.UstIdPruefen.Replace("{id}", id.ToString(), StringComparison.Ordinal);

    private sealed class FakeVies : IViesClient
    {
        public ViesErgebnis Ergebnis { get; set; } = new(UstIdStatus.Gueltig);

        public List<(string Land, string Nummer)> Anfragen { get; } = [];

        public Task<ViesErgebnis> PruefenAsync(string land, string nummer, CancellationToken ct)
        {
            Anfragen.Add((land, nummer));
            return Task.FromResult(Ergebnis);
        }
    }
}
