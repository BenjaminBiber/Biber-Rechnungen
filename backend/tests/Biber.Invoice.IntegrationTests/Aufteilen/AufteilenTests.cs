using System.Net;
using System.Security.Cryptography;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Aufteilen;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;

namespace Biber.Invoice.IntegrationTests.Aufteilen;

[Collection("Api")]
public sealed class AufteilenTests(ApiFabrik fabrik)
{
    private static readonly AufteilenDto ZweiTeile = new([new([1, 2]), new([3, 4])]);

    [Fact]
    public async Task Teilt_in_zwei_verknuepfte_Belege_und_laesst_das_Original_unveraendert()
    {
        var (f, belegId) = await VierseitigenBelegAsync();
        var original = await OriginalAsync(f, belegId);
        var pfad = Path.Combine(f.TempOrdner, "ablage", original.Pfad);
        var hashVorher = Sha256(await File.ReadAllBytesAsync(pfad));

        var antwort = await f.ErzeugeClient(Rolle.Benutzer).PostJsonAsync(Route(belegId), ZweiTeile);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var vorgaenge = (await antwort.Content.LesenAsync<ImportVorgangDto[]>())!;
        vorgaenge.Length.ShouldBe(2);

        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var teile = await db.Belege.AsNoTracking().Where(b => b.AufgeteiltAusBelegId == belegId).ToListAsync();
        teile.Count.ShouldBe(2);
        teile.ShouldAllBe(b => b.Seitenanzahl == 2);
        foreach (var teil in teile)
        {
            var datei = await db.BelegDateien.AsNoTracking().SingleAsync(d => d.BelegId == teil.Id && d.Rolle == DateiRolle.Original);
            using var pdf = PdfDocument.Open(Path.Combine(f.TempOrdner, "ablage", datei.Pfad));
            pdf.NumberOfPages.ShouldBe(2);
        }

        Sha256(await File.ReadAllBytesAsync(pfad)).ShouldBe(hashVorher);
        (await OriginalAsync(f, belegId)).Sha256.ShouldBe(original.Sha256);
        var audit = await db.AuditEintraege.AsNoTracking()
            .SingleAsync(a => a.BelegId == belegId && a.Aktion == AufteilenDienst.AuditAktion);
        audit.NeuerWert.ShouldBe("Aufgeteilt in 2 Belege");
    }

    public static TheoryData<int[][]> UngueltigeTeile() => new()
    {
        new[] { new[] { 1, 2 }, new[] { 2, 3, 4 } },
        new[] { new[] { 1, 2 }, new[] { 3 } },
        new[] { new[] { 1, 2 }, new[] { 3, 4, 5 } },
        new[] { new[] { 1, 2, 3, 4 } },
        new[] { new[] { 1, 2, 3, 4 }, Array.Empty<int>() },
    };

    [Theory]
    [MemberData(nameof(UngueltigeTeile))]
    public async Task Ungueltige_Seitenbereiche_werden_mit_400_abgelehnt(int[][] teile)
    {
        var (f, belegId) = await VierseitigenBelegAsync();

        var antwort = await f.ErzeugeClient().PostJsonAsync(Route(belegId), new AufteilenDto([.. teile.Select(s => new AufteilenTeilDto(s))]));

        antwort.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await TeileAsync(f, belegId)).ShouldBe(0);
    }

    [Fact]
    public async Task Zweites_Aufteilen_ergibt_409()
    {
        var (f, belegId) = await VierseitigenBelegAsync();
        var client = f.ErzeugeClient();
        (await client.PostJsonAsync(Route(belegId), ZweiTeile)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var antwort = await client.PostJsonAsync(Route(belegId), ZweiTeile);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await TeileAsync(f, belegId)).ShouldBe(2);
    }

    [Fact]
    public async Task XML_Beleg_ergibt_409()
    {
        var beleg = Testdaten.Beleg();
        beleg.Seitenanzahl = 4;
        beleg = await fabrik.SpeichernAsync(beleg);
        await fabrik.SpeichernAsync(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Rolle = DateiRolle.Original,
            Pfad = $"xml/{Guid.NewGuid():N}.xml",
            Sha256 = new string('a', 64),
            MimeType = "application/xml",
            AbgelegtAm = fabrik.Zeit.GetUtcNow(),
        });

        var antwort = await fabrik.ErzeugeClient().PostJsonAsync(Route(beleg.Id), ZweiTeile);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NurLesen_ergibt_403()
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).PostJsonAsync(Route(Guid.NewGuid()), ZweiTeile);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unbekannter_Beleg_ergibt_404()
    {
        var antwort = await fabrik.ErzeugeClient().PostJsonAsync(Route(Guid.NewGuid()), ZweiTeile);

        antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static string Route(Guid id) => ApiRouten.Belege.Aufteilen.Replace("{id}", id.ToString());

    /// <summary>Importiert ein vierseitiges PDF mit eindeutigem Inhalt durch die echte Pipeline.</summary>
    private async Task<(ApiFabrik Fabrik, Guid BelegId)> VierseitigenBelegAsync()
    {
        var f = fabrik.MitSofortVerarbeitung();
        await TestbenutzerAsync(f, Rolle.Admin);
        await TestbenutzerAsync(f, Rolle.Benutzer);
        var vorgang = await f.ImportiereAsync("scan.pdf", VierseitigesPdf());
        vorgang.BelegId.ShouldNotBeNull($"Import endete mit {vorgang.Status}: {vorgang.Ergebnis}");
        return (f, vorgang.BelegId.Value);
    }

    /// <summary>Legt den Benutzer des Test-Clients an; der Import-Vorgang verweist per Fremdschlüssel auf ihn.</summary>
    private static async Task TestbenutzerAsync(ApiFabrik f, Rolle rolle)
    {
        var id = ApiFabrik.BenutzerId(rolle);
        using (var scope = f.ErzeugeScope())
        {
            if (await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Users.AnyAsync(b => b.Id == id))
            {
                return;
            }
        }

        var benutzer = Testdaten.Benutzer();
        benutzer.Id = id;
        await f.SpeichernAsync(benutzer);
    }

    private static async Task<BelegDatei> OriginalAsync(ApiFabrik f, Guid belegId)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.BelegDateien.AsNoTracking().SingleAsync(d => d.BelegId == belegId && d.Rolle == DateiRolle.Original);
    }

    private static async Task<int> TeileAsync(ApiFabrik f, Guid belegId)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Belege.CountAsync(b => b.AufgeteiltAusBelegId == belegId);
    }

    private static byte[] VierseitigesPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
        var kennung = Guid.NewGuid().ToString("N");
        return Document.Create(dokument =>
        {
            for (var i = 1; i <= 4; i++)
            {
                var n = i;
                dokument.Page(seite => seite.Content().Text($"Seite {n} {kennung}"));
            }
        }).GeneratePdf();
    }

    private static string Sha256(byte[] inhalt) => Convert.ToHexStringLower(SHA256.HashData(inhalt));
}
