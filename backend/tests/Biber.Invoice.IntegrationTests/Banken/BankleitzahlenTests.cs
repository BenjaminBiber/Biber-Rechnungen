using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Banken;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Biber.Invoice.IntegrationTests.Banken;

/// <summary>Bankleitzahlen hochladen, bei der (nachgebildeten) Bundesbank abrufen und als Bankname zur IBAN zeigen.</summary>
[Collection("Api")]
public sealed class BankleitzahlenTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private const string Raiffeisen = "DE55733692640000043591";
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(Jetzt);
        await fabrik.ZuruecksetzenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Hochladen_ersetzt_die_Banken_und_der_Beleg_zeigt_den_Banknamen()
    {
        var beleg = Testdaten.Beleg();
        beleg.Bankverbindungen = [new Bankverbindung(Raiffeisen, "GENODEF1DTA"), new Bankverbindung("GB82WEST12345698765432")];
        await fabrik.SpeichernAsync(beleg);
        var client = fabrik.ErzeugeClient();

        var antwort = await client.PutAsync(ApiRouten.Bankleitzahlen.Stand, Formular(Datei(Satz("73369264", "Raiffeisenbank im Allgäuer Land"))));

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stand = (await antwort.Content.LesenAsync<BankleitzahlenStandDto>()).ShouldNotBeNull();
        stand.ShouldBe(new BankleitzahlenStandDto(Fueller + 1, Jetzt, BankleitzahlenQuelle.Upload, "blz-aktuell-txt-data.txt", null, null));
        (await BankenAsync(beleg.Id)).ShouldBe(["Raiffeisenbank im Allgäuer Land", null]);

        (await client.PutAsync(ApiRouten.Bankleitzahlen.Stand, Formular(Datei()))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await BankenAsync(beleg.Id)).ShouldBe([null, null]);
        (await StandAsync(client)).Anzahl.ShouldBe(Fueller);
    }

    [Fact]
    public async Task Falsche_Datei_ergibt_400_und_laesst_die_Banken_stehen()
    {
        var client = fabrik.ErzeugeClient();
        (await client.PutAsync(ApiRouten.Bankleitzahlen.Stand, Formular(Datei()))).EnsureSuccessStatusCode();

        var antwort = await client.PutAsync(ApiRouten.Bankleitzahlen.Stand, Formular("Bankleitzahl;Merkmal;Bezeichnung"u8.ToArray(), "blz.csv"));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await antwort.Content.ReadAsStringAsync()).ShouldContain("keine Bankleitzahlendatei");
        (await StandAsync(client)).Anzahl.ShouldBe(Fueller);
    }

    [Theory]
    [InlineData(Rolle.Benutzer)]
    [InlineData(Rolle.NurLesen)]
    public async Task Nur_Admins_duerfen_die_Bankleitzahlen_sehen_und_aendern(Rolle rolle)
    {
        var client = fabrik.ErzeugeClient(rolle);

        (await client.GetAsync(ApiRouten.Bankleitzahlen.Stand)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PutAsync(ApiRouten.Bankleitzahlen.Stand, Formular(Datei()))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync(ApiRouten.Bankleitzahlen.Abrufen, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Abrufen_holt_die_Datei_von_der_Bundesbank()
    {
        var (api, bundesbank) = MitBundesbank();
        bundesbank.Inhalt = Datei(Satz("73369264", "Raiffeisenbank im Allgäuer Land"));

        var antwort = await api.ErzeugeClient().PostAsync(ApiRouten.Bankleitzahlen.Abrufen, null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<BankleitzahlenStandDto>()).ShouldBe(new BankleitzahlenStandDto(
            Fueller + 1, Jetzt, BankleitzahlenQuelle.Bundesbank, bundesbank.Adresse.ToString(), Jetzt, null));
        using var scope = api.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Banken.SingleAsync(b => b.Blz == "73369264")).Name.ShouldBe("Raiffeisenbank im Allgäuer Land");
    }

    [Fact]
    public async Task Fehler_beim_Abruf_steht_im_Stand_und_die_Banken_bleiben()
    {
        var (api, bundesbank) = MitBundesbank();
        var client = api.ErzeugeClient();
        (await client.PutAsync(ApiRouten.Bankleitzahlen.Stand, Formular(Datei()))).EnsureSuccessStatusCode();
        bundesbank.Fehler = "Bundesbank nicht erreichbar – Downloadseite konnte nicht geladen werden.";

        var stand = (await (await client.PostAsync(ApiRouten.Bankleitzahlen.Abrufen, null)).Content.LesenAsync<BankleitzahlenStandDto>())
            .ShouldNotBeNull();

        stand.Anzahl.ShouldBe(Fueller);
        stand.Quelle.ShouldBe(BankleitzahlenQuelle.Upload);
        stand.LetzterAbrufAm.ShouldBe(Jetzt);
        stand.LetzterFehler.ShouldBe(bundesbank.Fehler);
    }

    [Fact]
    public async Task Job_holt_stuendlich_solange_die_Tabelle_leer_ist_und_dann_nur_neue_Dateien()
    {
        var (api, bundesbank) = MitBundesbank();
        bundesbank.Fehler = "Bundesbank nicht erreichbar.";

        await JobAsync(api);
        bundesbank.Abfragen.ShouldBe(1);
        api.Zeit.Vorspulen(TimeSpan.FromMinutes(30));
        await JobAsync(api);
        bundesbank.Abfragen.ShouldBe(1); // zu früh für den nächsten Versuch

        bundesbank.Fehler = null;
        api.Zeit.Vorspulen(TimeSpan.FromMinutes(30));
        await JobAsync(api);
        (bundesbank.Abfragen, bundesbank.Downloads).ShouldBe((2, 1));
        (await StandAsync(api.ErzeugeClient())).ShouldBe(new BankleitzahlenStandDto(
            Fueller, api.Zeit.GetUtcNow(), BankleitzahlenQuelle.Bundesbank, bundesbank.Adresse.ToString(), api.Zeit.GetUtcNow(), null));

        api.Zeit.Vorspulen(TimeSpan.FromDays(1));
        await JobAsync(api);
        bundesbank.Abfragen.ShouldBe(2); // mit Banken nur wöchentlich

        api.Zeit.Vorspulen(TimeSpan.FromDays(7));
        await JobAsync(api);
        (bundesbank.Abfragen, bundesbank.Downloads).ShouldBe((3, 1)); // dieselbe Datei wird nicht noch einmal geladen

        bundesbank.Adresse = new Uri("https://bundesbank.test/resource/blob/602632/neu/blz-aktuell-txt-data.txt");
        api.Zeit.Vorspulen(TimeSpan.FromDays(7));
        await JobAsync(api);
        (bundesbank.Abfragen, bundesbank.Downloads).ShouldBe((4, 2));
    }

    private const int Fueller = BankleitzahlenDatei.MindestAnzahl;

    private (ApiFabrik Api, FakeBundesbank Bundesbank) MitBundesbank()
    {
        var bundesbank = new FakeBundesbank();
        var api = fabrik.MitDiensten(d =>
        {
            d.RemoveAll<IBundesbankClient>();
            d.AddSingleton<IBundesbankClient>(bundesbank);
        });
        api.Zeit.Setze(Jetzt);
        return (api, bundesbank);
    }

    private static async Task JobAsync(ApiFabrik api)
    {
        using var scope = api.ErzeugeScope();
        await scope.ServiceProvider.GetServices<IWiederkehrenderJob>().OfType<BankleitzahlenJob>().Single()
            .AusfuehrenAsync(CancellationToken.None);
    }

    private static async Task<BankleitzahlenStandDto> StandAsync(HttpClient client) =>
        (await (await client.GetAsync(ApiRouten.Bankleitzahlen.Stand)).Content.LesenAsync<BankleitzahlenStandDto>()).ShouldNotBeNull();

    private async Task<string?[]> BankenAsync(Guid belegId)
    {
        var antwort = await fabrik.ErzeugeClient().GetAsync(ApiRouten.Belege.Einzeln.Replace("{id}", belegId.ToString(), StringComparison.Ordinal));
        var detail = (await antwort.Content.LesenAsync<BelegDetailDto>()).ShouldNotBeNull();
        return [.. detail.Bankverbindungen.Select(b => b.Bank)];
    }

    private static MultipartFormDataContent Formular(byte[] inhalt, string dateiname = "blz-aktuell-txt-data.txt")
    {
        var datei = new ByteArrayContent(inhalt);
        datei.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        return new MultipartFormDataContent { { datei, "datei", dateiname } };
    }

    /// <summary>Bankleitzahlendatei im Format der Bundesbank (ISO-8859-1): Füllbanken plus <paramref name="saetze"/>.</summary>
    private static byte[] Datei(params string[] saetze)
    {
        var fueller = Enumerable.Range(0, Fueller).Select(i => Satz($"1{i:D7}", $"Testbank {i}"));
        return Encoding.Latin1.GetBytes(string.Join("\r\n", fueller.Concat(saetze)) + "\r\n");
    }

    /// <summary>Hauptstelle mit 168 Zeichen: BLZ, Merkmal 1, Bezeichnung (58), PLZ, Ort, Kurzbezeichnung, PAN, BIC, …</summary>
    private static string Satz(string blz, string name) =>
        blz + "1" + name.PadRight(58) + "10115" + "Berlin".PadRight(35) + name.PadRight(27)[..27]
        + "     " + "TESTDEFFXXX" + "00" + "000001" + "U" + "0" + "00000000";

    private sealed class FakeBundesbank : IBundesbankClient
    {
        public Uri Adresse { get; set; } = new("https://bundesbank.test/resource/blob/602632/alt/blz-aktuell-txt-data.txt");
        public byte[] Inhalt { get; set; } = Datei();
        public string? Fehler { get; set; }
        public int Abfragen { get; private set; }
        public int Downloads { get; private set; }

        public Task<Uri> DateiAdresseAsync(CancellationToken ct)
        {
            Abfragen++;
            return Fehler is null ? Task.FromResult(Adresse) : throw new BankleitzahlenAusnahme(Fehler);
        }

        public Task<byte[]> LadenAsync(Uri adresse, CancellationToken ct)
        {
            Downloads++;
            return Task.FromResult(Inhalt);
        }
    }
}
