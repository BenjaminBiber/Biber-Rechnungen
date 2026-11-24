using System.Security.Cryptography;
using System.Text;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Ablage;
using Biber.Invoice.Infrastructure.Module.Formaterkennung;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class AblageTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Import_legt_das_Original_schreibgeschuetzt_ab_und_leert_das_Staging()
    {
        var f = OhneFormaterkennung();
        var inhalt = EindeutigerInhalt();

        var vorgang = await f.ImportiereAsync("rechnung.pdf", inhalt);

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        vorgang.StagingPfad.ShouldBeNull();
        Directory.EnumerateFiles(Path.Combine(f.TempOrdner, "staging"), "*", SearchOption.AllDirectories).ShouldBeEmpty();

        var datei = (await DateienAsync(f, vorgang.BelegId!.Value)).ShouldHaveSingleItem();
        datei.Rolle.ShouldBe(DateiRolle.Original);
        datei.Sha256.ShouldBe(vorgang.Sha256);
        datei.Sha256.ShouldBe(Sha256(inhalt));
        datei.Groesse.ShouldBe(inhalt.Length);
        datei.MimeType.ShouldBe("application/pdf");
        datei.AbgelegtAm.ShouldBe(f.Zeit.GetUtcNow());
        var datum = DateOnly.FromDateTime(f.Zeit.GetUtcNow().UtcDateTime);
        datei.Pfad.ShouldBe($"{datum.Year}/_Unbekannt/{datum:yyyy-MM-dd}_ohne-Nummer.pdf");

        var absolut = Path.Combine(f.TempOrdner, "ablage", datei.Pfad);
        (await File.ReadAllBytesAsync(absolut)).ShouldBe(inhalt);
        IstSchreibgeschuetzt(absolut).ShouldBeTrue();
        Should.Throw<UnauthorizedAccessException>(() => File.WriteAllBytes(absolut, [1, 2, 3]));
    }

    [Fact]
    public async Task Pfad_folgt_dem_Muster_aus_Rechnungsdatum_Korrespondent_und_Nummer()
    {
        var (datei, f) = await AblegenAsync("Bürowelt Krämer KG", "RE-2026-0912", new DateOnly(2025, 12, 30));

        datei.Pfad.ShouldBe("2025/Buerowelt_Kraemer_KG/2025-12-30_RE-2026-0912.pdf");
        File.Exists(Path.Combine(f.TempOrdner, "ablage", datei.Pfad)).ShouldBeTrue();
    }

    [Fact]
    public async Task Zweiter_Beleg_mit_gleichem_Pfad_erhaelt_Suffix_und_ueberschreibt_nichts()
    {
        var korrespondent = $"Kollision {Guid.NewGuid():N}";
        var (erste, f) = await AblegenAsync(korrespondent, "RE-1", new DateOnly(2026, 2, 1));
        var (zweite, _) = await AblegenAsync(korrespondent, "RE-1", new DateOnly(2026, 2, 1), f);
        var (dritte, _) = await AblegenAsync(korrespondent, "RE-1", new DateOnly(2026, 2, 1), f);

        zweite.Pfad.ShouldBe(erste.Pfad.Replace(".pdf", "_2.pdf"));
        dritte.Pfad.ShouldBe(erste.Pfad.Replace(".pdf", "_3.pdf"));
        var ablage = Path.Combine(f.TempOrdner, "ablage");
        Sha256(await File.ReadAllBytesAsync(Path.Combine(ablage, erste.Pfad))).ShouldBe(erste.Sha256);
        Sha256(await File.ReadAllBytesAsync(Path.Combine(ablage, zweite.Pfad))).ShouldBe(zweite.Sha256);
        erste.Sha256.ShouldNotBe(zweite.Sha256);
    }

    [Fact]
    public async Task Zugferd_legt_PDF_und_eingebettetes_XML_ab()
    {
        var xml = Encoding.UTF8.GetBytes("<rsm:CrossIndustryInvoice/>");
        var (_, f, belegId) = await AblegenMitBelegAsync("ZUGFeRD GmbH", $"ZF-{Guid.NewGuid():N}"[..12], new DateOnly(2026, 4, 2),
            k => { k.Format = ErkanntesFormat.ZugferdPdf; k.ERechnungXml = xml; });

        var dateien = await DateienAsync(f, belegId);
        dateien.Count.ShouldBe(2);
        var pdf = dateien.Single(d => d.Rolle == DateiRolle.Original);
        var eingebettet = dateien.Single(d => d.Rolle == DateiRolle.EingebettetesXml);
        eingebettet.Pfad.ShouldBe(Path.ChangeExtension(pdf.Pfad, "xml"));
        eingebettet.MimeType.ShouldBe("application/xml");
        eingebettet.Sha256.ShouldBe(Sha256(xml));
        (await File.ReadAllBytesAsync(Path.Combine(f.TempOrdner, "ablage", eingebettet.Pfad))).ShouldBe(xml);
    }

    [Fact]
    public async Task Wiederholung_schreibt_nichts_Neues_und_Pfad_bleibt_trotz_neuem_Korrespondenten()
    {
        var (datei, f, belegId) = await AblegenMitBelegAsync("Alter Name", "RE-77", new DateOnly(2026, 5, 5));
        var anderer = await f.SpeichernAsync(Testdaten.Korrespondent("Neuer Name"));
        var dateienVorher = Directory.EnumerateFiles(Path.Combine(f.TempOrdner, "ablage"), "*", SearchOption.AllDirectories).Count();

        using (var scope = f.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            var beleg = await db.Belege.SingleAsync(b => b.Id == belegId);
            beleg.KorrespondentId = anderer.Id;
            var staging = await StagingAsync(scope, EindeutigerInhalt());
            var kontext = Kontext(beleg, staging);
            (await Schritt(scope).AusfuehrenAsync(kontext, CancellationToken.None)).ShouldBe(SchrittErgebnis.Weiter);
            await db.SaveChangesAsync();
        }

        var dateien = await DateienAsync(f, belegId);
        dateien.ShouldHaveSingleItem().Pfad.ShouldBe(datei.Pfad);
        Directory.EnumerateFiles(Path.Combine(f.TempOrdner, "ablage"), "*", SearchOption.AllDirectories).Count().ShouldBe(dateienVorher);
    }

    [Fact]
    public async Task Pfad_verlaesst_die_Ablage_nie()
    {
        var (datei, f) = await AblegenAsync("..", "../../etc", new DateOnly(2026, 6, 1));

        datei.Pfad.ShouldBe("2026/_Unbekannt/2026-06-01_etc.pdf");
        var wurzel = Path.GetFullPath(Path.Combine(f.TempOrdner, "ablage")) + Path.DirectorySeparatorChar;
        Path.GetFullPath(Path.Combine(wurzel, datei.Pfad)).ShouldStartWith(wurzel);
    }

    [Fact]
    public async Task Abweichender_Hash_bricht_den_Schritt_ab()
    {
        var f = fabrik;
        using var scope = f.ErzeugeScope();
        var beleg = await f.SpeichernAsync(Testdaten.Beleg());
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var geladen = await db.Belege.SingleAsync(b => b.Id == beleg.Id);
        var staging = await StagingAsync(scope, EindeutigerInhalt());
        var kontext = Kontext(geladen, staging, sha256: new string('0', 64));

        await Should.ThrowAsync<InvalidOperationException>(() => Schritt(scope).AusfuehrenAsync(kontext, CancellationToken.None));
        File.Exists(staging.Pfad).ShouldBeTrue();
    }

    private async Task<(BelegDatei Datei, ApiFabrik Fabrik)> AblegenAsync(
        string korrespondent, string nummer, DateOnly datum, ApiFabrik? f = null)
    {
        var (datei, fab, _) = await AblegenMitBelegAsync(korrespondent, nummer, datum, f: f);
        return (datei, fab);
    }

    private async Task<(BelegDatei Datei, ApiFabrik Fabrik, Guid BelegId)> AblegenMitBelegAsync(
        string korrespondentName,
        string nummer,
        DateOnly datum,
        Action<VerarbeitungsKontext>? anpassen = null,
        ApiFabrik? f = null)
    {
        f ??= fabrik;
        var korrespondent = await f.SpeichernAsync(Testdaten.Korrespondent(korrespondentName));
        var beleg = await f.SpeichernAsync(Testdaten.Beleg(nummer, datum, korrespondentId: korrespondent.Id));

        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var geladen = await db.Belege.SingleAsync(b => b.Id == beleg.Id);
        var staging = await StagingAsync(scope, EindeutigerInhalt());
        var kontext = Kontext(geladen, staging);
        kontext.Format = ErkanntesFormat.Pdf;
        anpassen?.Invoke(kontext);

        (await Schritt(scope).AusfuehrenAsync(kontext, CancellationToken.None)).ShouldBe(SchrittErgebnis.Weiter);
        await db.SaveChangesAsync();

        File.Exists(staging.Pfad).ShouldBeFalse();
        kontext.Vorgang.StagingPfad.ShouldBeNull();
        var original = (await DateienAsync(f, beleg.Id)).Single(d => d.Rolle == DateiRolle.Original);
        original.Sha256.ShouldBe(staging.Sha256);
        return (original, f, beleg.Id);
    }

    private static VerarbeitungsKontext Kontext(Beleg beleg, StagingDatei staging, string? sha256 = null) => new()
    {
        Vorgang = new ImportVorgang
        {
            Id = Guid.NewGuid(),
            Dateiname = "rechnung.pdf",
            Sha256 = sha256 ?? staging.Sha256,
            StagingPfad = staging.Pfad,
            Status = ImportStatus.InVerarbeitung,
        },
        Beleg = beleg,
        DateiPfad = staging.Pfad,
        Dateiname = "rechnung.pdf",
    };

    private static AblageSchritt Schritt(IServiceScope scope) =>
        scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<AblageSchritt>().Single();

    private static async Task<StagingDatei> StagingAsync(IServiceScope scope, byte[] inhalt)
    {
        using var strom = new MemoryStream(inhalt);
        return await scope.ServiceProvider.GetRequiredService<IStagingSpeicher>().SpeichernAsync(strom, "rechnung.pdf", CancellationToken.None);
    }

    private static async Task<List<BelegDatei>> DateienAsync(ApiFabrik f, Guid belegId)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.BelegDateien.AsNoTracking().Where(d => d.BelegId == belegId).ToListAsync();
    }

    private static bool IstSchreibgeschuetzt(string pfad) =>
        OperatingSystem.IsWindows()
            ? File.GetAttributes(pfad).HasFlag(FileAttributes.ReadOnly)
            : (File.GetUnixFileMode(pfad) & (UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0;

    /// <summary>Sofort-Verarbeitung ohne die echte Formaterkennung: Sie würde die Platzhalter-PDFs ablehnen.</summary>
    private ApiFabrik OhneFormaterkennung() =>
        fabrik.MitSofortVerarbeitung().MitDiensten(d =>
        {
            foreach (var eintrag in d.Where(e => e.ServiceType == typeof(IPipelineSchritt) && e.ImplementationType == typeof(FormaterkennungSchritt)).ToList())
            {
                d.Remove(eintrag);
            }
        });

    private static byte[] EindeutigerInhalt() => Encoding.UTF8.GetBytes($"%PDF-1.7 {Guid.NewGuid():N}");

    private static string Sha256(byte[] inhalt) => Convert.ToHexStringLower(SHA256.HashData(inhalt));
}
