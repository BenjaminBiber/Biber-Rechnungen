using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.NeuAuslesen;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.NeuAuslesen;

[Collection("Api")]
public sealed class NeuAuslesenTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateTimeOffset Jetzt = new(2026, 2, 1, 9, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(Jetzt);
        await fabrik.ZuruecksetzenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Start_zaehlt_Belege_mit_Original_reiht_den_Job_ein_und_ergibt_409_solange_er_laeuft()
    {
        var (api, starter, _) = Testaufbau();
        await BelegAsync();
        await BelegAsync();
        await fabrik.SpeichernAsync(Testdaten.Beleg()); // ohne Original
        await BelegAsync(b => b.GeloeschtAm = Jetzt); // gelöscht
        var client = api.ErzeugeClient();

        var vorher = await StatusAsync(client);
        vorher.AnzahlBelege.ShouldBe(2);
        vorher.Lauf.ShouldBeNull();

        var antwort = await client.PostAsync(ApiRouten.NeuAuslesen.Starten, null);
        antwort.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var lauf = (await antwort.Content.ReadFromJsonAsync<NeuAuslesenStatusDto>()).ShouldNotBeNull().Lauf.ShouldNotBeNull();
        lauf.Laeuft.ShouldBeTrue();
        lauf.Gesamt.ShouldBe(2);
        lauf.Verarbeitet.ShouldBe(0);
        lauf.RestSekunden.ShouldBeNull();
        starter.Laeufe.ShouldBe([lauf.Id]);

        (await client.PostAsync(ApiRouten.NeuAuslesen.Starten, null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        starter.Laeufe.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(Rolle.Benutzer)]
    [InlineData(Rolle.NurLesen)]
    public async Task Nur_Admins_duerfen_neu_auslesen(Rolle rolle)
    {
        var (api, starter, _) = Testaufbau();
        var client = api.ErzeugeClient(rolle);

        (await client.GetAsync(ApiRouten.NeuAuslesen.Status)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync(ApiRouten.NeuAuslesen.Starten, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync(ApiRouten.NeuAuslesen.Abbrechen, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        starter.Laeufe.ShouldBeEmpty();
    }

    [Fact]
    public async Task Job_liest_alle_Belege_aus_zaehlt_Fehler_und_ueberspringt_Belege_in_Verarbeitung()
    {
        var (api, _, verarbeitung) = Testaufbau();
        var ok = await BelegAsync();
        var kaputt = await BelegAsync();
        var inVerarbeitung = await BelegAsync(b => b.Status = BelegStatus.InVerarbeitung);
        verarbeitung.Fehler[kaputt.Id] = new InvalidOperationException("Original nicht lesbar");
        var client = api.ErzeugeClient();
        var laufId = (await StartenAsync(client)).Id;

        await JobAsync(api, laufId);

        verarbeitung.Aufrufe.ShouldBe([ok.Id, kaputt.Id], ignoreOrder: true);
        var lauf = (await StatusAsync(client)).Lauf.ShouldNotBeNull();
        lauf.Laeuft.ShouldBeFalse();
        lauf.BeendetAm.ShouldBe(Jetzt);
        lauf.Abgebrochen.ShouldBeFalse();
        lauf.Abbruchgrund.ShouldBeNull();
        lauf.Gesamt.ShouldBe(3);
        lauf.Verarbeitet.ShouldBe(3);
        lauf.AnzahlFehler.ShouldBe(1);
        lauf.AnzahlUebersprungen.ShouldBe(1);
        lauf.FehlerBelege.ShouldBe([kaputt.Id]);
        lauf.LetzterFehler.ShouldBe("Original nicht lesbar");
        lauf.RestSekunden.ShouldBeNull();
        verarbeitung.Aufrufe.ShouldNotContain(inVerarbeitung.Id);
    }

    [Fact]
    public async Task Erreichtes_DI_Limit_haelt_den_Lauf_an()
    {
        var (api, _, verarbeitung) = Testaufbau();
        for (var i = 0; i < 4; i++)
        {
            await BelegAsync();
        }

        verarbeitung.AbAufruf(2, new VerarbeitungAbgebrochenException("Document Intelligence", SchrittAusgang.Pausiert, "Monatslimit erreicht"));
        var client = api.ErzeugeClient();
        var laufId = (await StartenAsync(client)).Id;

        await JobAsync(api, laufId);

        verarbeitung.Aufrufe.Count.ShouldBe(2);
        var lauf = (await StatusAsync(client)).Lauf.ShouldNotBeNull();
        lauf.Laeuft.ShouldBeFalse();
        lauf.Verarbeitet.ShouldBe(2);
        lauf.Gesamt.ShouldBe(4);
        lauf.AnzahlFehler.ShouldBe(1);
        lauf.Abbruchgrund.ShouldBe("Document Intelligence: Monatslimit erreicht");
    }

    [Fact]
    public async Task Abbrechen_beendet_den_Lauf_vor_dem_naechsten_Beleg()
    {
        var (api, _, verarbeitung) = Testaufbau();
        await BelegAsync();
        await BelegAsync();
        var client = api.ErzeugeClient();

        (await client.PostAsync(ApiRouten.NeuAuslesen.Abbrechen, null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var laufId = (await StartenAsync(client)).Id;

        var antwort = await client.PostAsync(ApiRouten.NeuAuslesen.Abbrechen, null);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var abgebrochen = (await antwort.Content.ReadFromJsonAsync<NeuAuslesenStatusDto>()).ShouldNotBeNull().Lauf.ShouldNotBeNull();
        abgebrochen.Laeuft.ShouldBeTrue(); // der Job hört erst nach dem aktuellen Beleg auf
        abgebrochen.Abgebrochen.ShouldBeTrue();

        await JobAsync(api, laufId);

        verarbeitung.Aufrufe.ShouldBeEmpty();
        var lauf = (await StatusAsync(client)).Lauf.ShouldNotBeNull();
        lauf.Laeuft.ShouldBeFalse();
        lauf.BeendetAm.ShouldNotBeNull();
        lauf.Abbruchgrund.ShouldBe(NeuAuslesenDienst.GrundAbgebrochen);
        (await client.PostAsync(ApiRouten.NeuAuslesen.Starten, null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Restzeit_ergibt_sich_aus_dem_bisherigen_Tempo()
    {
        await fabrik.SpeichernAsync(new NeuAuslesenLauf
        {
            Id = Guid.NewGuid(),
            GestartetAm = Jetzt.AddMinutes(-2),
            AktualisiertAm = Jetzt.AddSeconds(-10),
            Gesamt = 10,
            Verarbeitet = 4,
        });

        var lauf = (await StatusAsync(fabrik.ErzeugeClient())).Lauf.ShouldNotBeNull();

        lauf.Laeuft.ShouldBeTrue();
        lauf.RestSekunden.ShouldBe(165); // 110 s für 4 Belege → 27,5 s je Beleg, 6 übrig
        lauf.DauerSekunden.ShouldBe(120);
    }

    [Fact]
    public async Task Haengender_Lauf_gilt_als_unterbrochen_und_sperrt_keinen_neuen()
    {
        var (api, starter, _) = Testaufbau();
        await BelegAsync();
        var alt = await fabrik.SpeichernAsync(new NeuAuslesenLauf
        {
            Id = Guid.NewGuid(),
            GestartetAm = Jetzt.AddHours(-2),
            AktualisiertAm = Jetzt - NeuAuslesenDienst.Haengezeit - TimeSpan.FromMinutes(1),
            Gesamt = 5,
            Verarbeitet = 2,
        });
        var client = api.ErzeugeClient();

        var haengend = (await StatusAsync(client)).Lauf.ShouldNotBeNull();
        haengend.Laeuft.ShouldBeFalse();
        haengend.BeendetAm.ShouldBeNull();
        haengend.Abbruchgrund.ShouldBe(NeuAuslesenDienst.GrundUnterbrochen);

        (await client.PostAsync(ApiRouten.NeuAuslesen.Starten, null)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

        starter.Laeufe.Count.ShouldBe(1);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var geschlossen = await db.NeuAuslesenLaeufe.SingleAsync(l => l.Id == alt.Id);
        geschlossen.BeendetAm.ShouldBe(Jetzt);
        geschlossen.Abbruchgrund.ShouldBe(NeuAuslesenDienst.GrundUnterbrochen);
    }

    [Fact]
    public async Task Fortgesetzter_Job_macht_beim_gespeicherten_Stand_weiter()
    {
        var (api, _, verarbeitung) = Testaufbau();
        for (var i = 0; i < 3; i++)
        {
            await BelegAsync();
        }

        var lauf = await fabrik.SpeichernAsync(new NeuAuslesenLauf
        {
            Id = Guid.NewGuid(),
            GestartetAm = Jetzt.AddMinutes(-1),
            AktualisiertAm = Jetzt.AddMinutes(-1),
            Gesamt = 3,
            Verarbeitet = 1,
        });
        List<Guid> reihenfolge;
        using (var scope = fabrik.ErzeugeScope())
        {
            reihenfolge = await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Belege
                .OrderBy(b => b.Id).Select(b => b.Id).ToListAsync();
        }

        await JobAsync(api, lauf.Id);

        verarbeitung.Aufrufe.ShouldBe(reihenfolge.Skip(1).ToList());
        var stand = (await StatusAsync(api.ErzeugeClient())).Lauf.ShouldNotBeNull();
        stand.Verarbeitet.ShouldBe(3);
        stand.Laeuft.ShouldBeFalse();
    }

    private (ApiFabrik Api, ZaehlStarter Starter, FakeVerarbeitung Verarbeitung) Testaufbau()
    {
        var starter = new ZaehlStarter();
        var verarbeitung = new FakeVerarbeitung();
        var api = fabrik.MitDiensten(d =>
        {
            d.RemoveAll<INeuAuslesenStarter>();
            d.AddSingleton<INeuAuslesenStarter>(starter);
            d.RemoveAll<IVerarbeitungsStarter>();
            d.AddSingleton<IVerarbeitungsStarter>(verarbeitung);
        });
        api.Zeit.Setze(Jetzt);
        return (api, starter, verarbeitung);
    }

    private static async Task JobAsync(ApiFabrik api, Guid laufId)
    {
        using var scope = api.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<NeuAuslesenJob>().AusfuehrenAsync(laufId, CancellationToken.None);
    }

    private static async Task<NeuAuslesenStatusDto> StatusAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<NeuAuslesenStatusDto>(ApiRouten.NeuAuslesen.Status)).ShouldNotBeNull();

    private static async Task<NeuAuslesenLaufDto> StartenAsync(HttpClient client)
    {
        var antwort = await client.PostAsync(ApiRouten.NeuAuslesen.Starten, null);
        antwort.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        return (await antwort.Content.ReadFromJsonAsync<NeuAuslesenStatusDto>()).ShouldNotBeNull().Lauf.ShouldNotBeNull();
    }

    private async Task<Beleg> BelegAsync(Action<Beleg>? anpassen = null)
    {
        var beleg = Testdaten.Beleg();
        beleg.Dateien.Add(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Rolle = DateiRolle.Original,
            Pfad = $"2026/test/{beleg.Id:N}.pdf",
            Sha256 = new string('0', 64),
            Groesse = 1,
            MimeType = "application/pdf",
            AbgelegtAm = Jetzt.AddDays(-10),
        });
        anpassen?.Invoke(beleg);
        return await fabrik.SpeichernAsync(beleg);
    }

    private sealed class ZaehlStarter : INeuAuslesenStarter
    {
        public List<Guid> Laeufe { get; } = [];

        public void Starten(Guid laufId) => Laeufe.Add(laufId);
    }

    private sealed class FakeVerarbeitung : IVerarbeitungsStarter
    {
        private int _abAufruf = int.MaxValue;
        private Exception? _abFehler;

        public List<Guid> Aufrufe { get; } = [];

        public Dictionary<Guid, Exception> Fehler { get; } = [];

        /// <summary>Ab dem <paramref name="nummer"/>. Aufruf (1-basiert) wirft jeder Aufruf <paramref name="fehler"/>.</summary>
        public void AbAufruf(int nummer, Exception fehler) => (_abAufruf, _abFehler) = (nummer, fehler);

        public Task ErneutVerarbeitenAsync(Guid belegId, CancellationToken ct)
        {
            Aufrufe.Add(belegId);
            if (Aufrufe.Count >= _abAufruf)
            {
                throw _abFehler!;
            }

            return Fehler.TryGetValue(belegId, out var fehler) ? Task.FromException(fehler) : Task.CompletedTask;
        }
    }
}
