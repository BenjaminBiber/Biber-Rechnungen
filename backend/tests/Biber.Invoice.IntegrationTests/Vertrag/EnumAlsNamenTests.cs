using System.Net;
using System.Text;
using System.Text.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.IntegrationTests.Auth;
using Biber.Invoice.IntegrationTests.Infrastruktur;

namespace Biber.Invoice.IntegrationTests.Vertrag;

/// <summary>Prüft das rohe JSON: Enums stehen wie in <c>docs/api/openapi.json</c> als Namen, nie als Zahl.</summary>
[Collection("Api")]
public sealed class EnumAlsNamenTests(ApiFabrik fabrik) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Beleg_Detail_und_Liste_liefern_Enums_als_Namen()
    {
        var beleg = Testdaten.Beleg();
        beleg.KorrespondentErkennung = KorrespondentErkennung.UstIdNr;
        await fabrik.SpeichernAsync(beleg);
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        using var detail = await RohAsync(client, ApiRouten.Belege.Einzeln.Replace("{id}", beleg.Id.ToString()));
        Name<KorrespondentErkennung>(detail.RootElement, "korrespondentErkennung").ShouldBe("UstIdNr");
        Name<BelegStatus>(detail.RootElement, "status");
        Name<ImportQuelle>(detail.RootElement, "quelle");

        using var liste = await RohAsync(client, ApiRouten.Belege.Liste);
        var eintrag = liste.RootElement.GetProperty("eintraege").EnumerateArray().ShouldHaveSingleItem();
        Name<ImportQuelle>(eintrag, "quelle");
    }

    [Fact]
    public async Task Import_Vorgang_liefert_Status_und_Quelle_als_Namen()
    {
        var vorgang = await fabrik.SpeichernAsync(new ImportVorgang
        {
            Id = Guid.NewGuid(),
            Quelle = ImportQuelle.Upload,
            Status = ImportStatus.Fertig,
            Dateiname = "rechnung.pdf",
            ErstelltAm = fabrik.Zeit.GetUtcNow(),
        });
        var client = fabrik.ErzeugeClient(Rolle.NurLesen);

        using var json = await RohAsync(client, ApiRouten.Import.Vorgang.Replace("{id}", vorgang.Id.ToString()));

        Name<ImportStatus>(json.RootElement, "status").ShouldBe("Fertig");
        Name<ImportQuelle>(json.RootElement, "quelle").ShouldBe("Upload");
    }

    [Fact]
    public async Task Benutzerliste_liefert_Rolle_als_Namen_und_nimmt_Namen_an()
    {
        var echt = fabrik.MitEchterAnmeldung();
        await echt.VorbereitenAsync();
        var adminKonto = await echt.BenutzerAnlegenAsync(Rolle.Admin);
        var anderer = await echt.BenutzerAnlegenAsync(Rolle.Benutzer);
        var admin = await echt.AngemeldetAsync(adminKonto);

        var pfad = ApiRouten.Benutzer.Einzeln.Replace("{id}", anderer.Id.ToString(), StringComparison.Ordinal);
        var antwort = await admin.PutAsync(
            pfad, new StringContent("""{"rolle":"NurLesen","freigegeben":true}""", Encoding.UTF8, "application/json"));
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var liste = await RohAsync(admin, ApiRouten.Benutzer.Liste);
        var rollen = liste.RootElement.EnumerateArray().Select(b => Name<Rolle>(b, "rolle")).ToList();
        rollen.ShouldBe(["Admin", "NurLesen"], ignoreOrder: true);
    }

    private static async Task<JsonDocument> RohAsync(HttpClient client, string pfad)
    {
        var antwort = await client.GetAsync(pfad);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK, pfad);
        return JsonDocument.Parse(await antwort.Content.ReadAsStringAsync());
    }

    /// <summary>Das Feld ist ein String und einer der Namen von <typeparamref name="T"/>.</summary>
    private static string Name<T>(JsonElement objekt, string feld)
        where T : struct, Enum
    {
        var wert = objekt.GetProperty(feld);
        wert.ValueKind.ShouldBe(JsonValueKind.String, $"{feld}: {wert.GetRawText()}");
        var name = wert.GetString()!;
        Enum.GetNames<T>().ShouldContain(name);
        return name;
    }
}
