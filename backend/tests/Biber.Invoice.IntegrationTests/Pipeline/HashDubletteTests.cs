using System.Security.Cryptography;
using System.Text;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Formaterkennung;
using Biber.Invoice.Infrastructure.Module.HashDublette;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class HashDubletteTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Zweiter_Import_derselben_Bytes_wird_Dublette_und_verweist_auf_das_Original()
    {
        var f = OhneFormaterkennung();
        var inhalt = EindeutigerInhalt();
        var original = await OriginalAnlegenAsync(f, inhalt, "RE-2026-0912", new DateOnly(2026, 1, 10));

        var vorgang = await f.ImportiereAsync("kopie.pdf", inhalt);

        vorgang.Status.ShouldBe(ImportStatus.Dublette);
        vorgang.Ergebnis.ShouldBe("Identische Datei (SHA-256) wie Beleg RE-2026-0912 vom 10.01.2026 – als Dublette verknüpft, nicht doppelt gezählt.");
        vorgang.BeendetAm.ShouldBe(f.Zeit.GetUtcNow());
        vorgang.Schritte.Select(s => s.Status).ShouldBe(["ok", "offen", "offen", "offen", "offen"]);
        vorgang.BelegId.ShouldNotBeNull();
        vorgang.BelegId.ShouldNotBe(original.Id);

        var beleg = await BelegAsync(f, vorgang.BelegId.Value);
        beleg.DuplikatVonId.ShouldBe(original.Id);
        beleg.Status.ShouldBe(BelegStatus.Fertig);
        beleg.Rechnungsnummer.ShouldBe(original.Rechnungsnummer);
        beleg.Rechnungsdatum.ShouldBe(original.Rechnungsdatum);
        beleg.KorrespondentId.ShouldBe(original.KorrespondentId);
        beleg.Netto.ShouldBe(original.Netto);
        beleg.Steuer.ShouldBe(original.Steuer);
        beleg.Brutto.ShouldBe(original.Brutto);

        vorgang.StagingPfad.ShouldBeNull();
        Directory.EnumerateFiles(Path.Combine(f.TempOrdner, "staging"), "*", SearchOption.AllDirectories).ShouldBeEmpty();
        Directory.Exists(Path.Combine(f.TempOrdner, "ablage")).ShouldBeFalse();
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.BelegDateien.CountAsync(d => d.BelegId == beleg.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Anderer_Inhalt_laeuft_weiter_und_bleibt_ohne_Verknuepfung()
    {
        var f = OhneFormaterkennung();
        await OriginalAnlegenAsync(f, EindeutigerInhalt(), "RE-2026-0100", new DateOnly(2026, 1, 10));

        var vorgang = await f.ImportiereAsync("neu.pdf", EindeutigerInhalt());

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        vorgang.Schritte.ShouldAllBe(s => s.Status == "ok");
        File.Exists(vorgang.StagingPfad!).ShouldBeTrue();
        (await BelegAsync(f, vorgang.BelegId!.Value)).DuplikatVonId.ShouldBeNull();
    }

    [Fact]
    public async Task Dritter_Import_verweist_auf_den_ersten_Beleg()
    {
        var f = OhneFormaterkennung();
        var inhalt = EindeutigerInhalt();
        var original = await OriginalAnlegenAsync(f, inhalt, "RE-2026-0200", new DateOnly(2026, 2, 1));

        var zweiter = await f.ImportiereAsync("zweiter.pdf", inhalt);
        var dritter = await f.ImportiereAsync("dritter.pdf", inhalt);

        zweiter.Status.ShouldBe(ImportStatus.Dublette);
        dritter.Status.ShouldBe(ImportStatus.Dublette);
        (await BelegAsync(f, zweiter.BelegId!.Value)).DuplikatVonId.ShouldBe(original.Id);
        (await BelegAsync(f, dritter.BelegId!.Value)).DuplikatVonId.ShouldBe(original.Id);
    }

    [Fact]
    public async Task Bei_einer_Kette_zeigt_der_Treffer_auf_den_aeltesten_Beleg()
    {
        var f = OhneFormaterkennung();
        var inhalt = EindeutigerInhalt();
        var wurzel = await OriginalAnlegenAsync(f, inhalt, "RE-2026-0300", new DateOnly(2026, 3, 1), tageAlt: 1);
        // Älter als die Wurzel, aber selbst Dublette (mit eigener Datei): der Treffer darf nicht auf ihn zeigen.
        await OriginalAnlegenAsync(f, inhalt, "RE-2026-0301", new DateOnly(2026, 3, 2), tageAlt: 2, duplikatVonId: wurzel.Id);

        var vorgang = await f.ImportiereAsync("neu.pdf", inhalt);

        vorgang.Status.ShouldBe(ImportStatus.Dublette);
        (await BelegAsync(f, vorgang.BelegId!.Value)).DuplikatVonId.ShouldBe(wurzel.Id);
    }

    [Fact]
    public async Task Wiederholung_ueberspringt_den_Schritt()
    {
        var f = OhneFormaterkennung();
        var inhalt = EindeutigerInhalt();
        await OriginalAnlegenAsync(f, inhalt, "RE-2026-0400", new DateOnly(2026, 4, 1));
        var beleg = Testdaten.Beleg();
        var vorgang = new ImportVorgang { Id = Guid.NewGuid(), Dateiname = "neu.pdf", Sha256 = Sha256(inhalt), Status = ImportStatus.Fertig };

        using var scope = f.ErzeugeScope();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<HashDubletteSchritt>().Single();
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = vorgang,
            Beleg = beleg,
            DateiPfad = Path.Combine(f.TempOrdner, "ablage", "irgendwo.pdf"),
            Dateiname = vorgang.Dateiname,
            IstWiederholung = true,
        };

        (await schritt.AusfuehrenAsync(kontext, CancellationToken.None)).ShouldBe(SchrittErgebnis.Weiter);
        beleg.DuplikatVonId.ShouldBeNull();
    }

    /// <summary>
    /// Sofort-Verarbeitung ohne die echte Formaterkennung: Sie würde die Platzhalter-PDFs ablehnen.
    /// </summary>
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

    private static async Task<Beleg> OriginalAnlegenAsync(
        ApiFabrik f,
        byte[] inhalt,
        string rechnungsnummer,
        DateOnly datum,
        int tageAlt = 0,
        Guid? duplikatVonId = null)
    {
        var korrespondent = await f.SpeichernAsync(Testdaten.Korrespondent());
        var beleg = Testdaten.Beleg(rechnungsnummer, datum, korrespondentId: korrespondent.Id);
        beleg.ErstelltAm = f.Zeit.GetUtcNow().AddDays(-tageAlt);
        beleg.DuplikatVonId = duplikatVonId;
        await f.SpeichernAsync(beleg);
        await f.SpeichernAsync(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Rolle = DateiRolle.Original,
            Pfad = Path.Combine("2026", "_Unbekannt", $"{rechnungsnummer}.pdf"),
            Sha256 = Sha256(inhalt),
            Groesse = inhalt.Length,
            MimeType = "application/pdf",
            AbgelegtAm = f.Zeit.GetUtcNow(),
        });
        return beleg;
    }

    private static async Task<Beleg> BelegAsync(ApiFabrik f, Guid id)
    {
        using var scope = f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Belege.AsNoTracking().SingleAsync(b => b.Id == id);
    }
}
