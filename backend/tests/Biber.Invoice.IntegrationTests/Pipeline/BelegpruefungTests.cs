using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.DocumentIntelligence;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class BelegpruefungTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly ImportHerkunft AusMail =
        new(MailMessageId: "<shop@haendler.test>", MailAbsender: "shop@haendler.test", MailBetreff: "Ihre Bestellung");

    private readonly FakeDiClient _di = new();
    private ApiFabrik _f = null!;

    public async Task InitializeAsync()
    {
        _f = fabrik.MitDi(_di);
        _f.Zeit.Setze(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero));
        await _f.ZuruecksetzenAsync();
        await _f.DiEinstellenAsync();
        var benutzer = Testdaten.Benutzer();
        benutzer.Id = ApiFabrik.BenutzerId(Rolle.Benutzer);
        await _f.SpeichernAsync(benutzer);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AGB_aus_einer_Mail_wird_ohne_DI_aussortiert_und_hinterlaesst_keinen_Beleg()
    {
        var vorgang = await _f.ImportiereAsync("AGB.pdf", DiTestHilfen.Pdf, ImportQuelle.Mail, AusMail);

        vorgang.Status.ShouldBe(ImportStatus.Aussortiert);
        vorgang.Ergebnis.ShouldBe("Kein Beleg: Dateiname deutet auf AGB");
        vorgang.BelegId.ShouldBeNull();
        vorgang.BeendetAm.ShouldNotBeNull();
        File.Exists(vorgang.StagingPfad).ShouldBeTrue();
        _di.Aufrufe.ShouldBe(0);
        (await BelegeAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task PDF_mit_AGB_Text_aus_dem_Ordner_wird_aussortiert()
    {
        var pdf = PdfMitText(
            "Allgemeine Geschaeftsbedingungen der Muster GmbH",
            "1. Geltungsbereich: Diese Bedingungen gelten fuer alle Bestellungen.",
            "5. Zahlung: Die Rechnung ist innerhalb von 14 Tagen zahlbar.");

        var vorgang = await _f.ImportiereAsync("dokument.pdf", pdf, ImportQuelle.Ordner, new ImportHerkunft(Pfad: "dokument.pdf"));

        vorgang.Status.ShouldBe(ImportStatus.Aussortiert);
        vorgang.Ergebnis.ShouldBe("Kein Beleg: Inhalt sieht nach AGB aus");
        _di.Aufrufe.ShouldBe(0);
    }

    [Fact]
    public async Task Rechnung_aus_einer_Mail_wird_Beleg()
    {
        var pdf = PdfMitText("Muster GmbH", "Rechnung", "Rechnungsnummer: RE-2026-17", "Gesamt 214,20 EUR inkl. 19 % MwSt.");

        var vorgang = await _f.ImportiereAsync("dokument.pdf", pdf, ImportQuelle.Mail, AusMail);

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        vorgang.BelegId.ShouldNotBeNull();
        _di.Aufrufe.ShouldBe(1);
    }

    [Theory]
    [InlineData(ImportQuelle.Upload)]
    [InlineData(ImportQuelle.Mcp)]
    public async Task Bewusst_hochgeladene_Dateien_werden_nie_aussortiert(ImportQuelle quelle)
    {
        var vorgang = await _f.ImportiereAsync("AGB.pdf", DiTestHilfen.Pdf, quelle);

        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        vorgang.BelegId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Scan_ohne_erkennbare_Rechnungsdaten_wird_nach_dem_Auslesen_aussortiert()
    {
        _di.Ergebnis = new ExtraktionsErgebnis
        {
            Quelle = ExtraktionsQuelle.DocumentIntelligence,
            Volltext = "Herzliche Gruesse aus dem Urlaub!",
            Seiten = 1,
        };

        var vorgang = await _f.ImportiereAsync("scan_0001.pdf", DiTestHilfen.Pdf, ImportQuelle.Ordner, new ImportHerkunft(Pfad: "scan_0001.pdf"));

        vorgang.Status.ShouldBe(ImportStatus.Aussortiert);
        vorgang.Ergebnis.ShouldBe("Kein Beleg: weder Betrag noch Rechnungsnummer oder Aussteller erkannt");
        vorgang.Schritte.Single(s => s.Name == "Inhalt prüfen").Status.ShouldBe("ok");
        _di.Aufrufe.ShouldBe(1);
        (await BelegeAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Aussortierte_Datei_laesst_sich_doch_als_Beleg_uebernehmen()
    {
        var aussortiert = await _f.ImportiereAsync("AGB.pdf", DiTestHilfen.Pdf, ImportQuelle.Mail, AusMail);
        var client = _f.ErzeugeClient(Rolle.Benutzer);

        var antwort = await client.PostAsync(AlsBeleg(aussortiert.Id), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await antwort.Content.LesenAsync<ImportVorgangDto>()).ShouldNotBeNull().Id.ShouldBe(aussortiert.Id);
        var vorgang = await _f.VorgangAsync(aussortiert.Id);
        vorgang.Status.ShouldBe(ImportStatus.Fertig);
        vorgang.AlsBelegBestaetigt.ShouldBeTrue();
        vorgang.Ergebnis.ShouldBe("1 Beleg angelegt");
        vorgang.StagingPfad.ShouldBeNull();
        var beleg = (await BelegeAsync()).ShouldHaveSingleItem();
        beleg.Id.ShouldBe(vorgang.BelegId!.Value);
        beleg.ImportVorgangId.ShouldBe(vorgang.Id);
        beleg.Quelle.ShouldBe(ImportQuelle.Mail);
        beleg.ErstelltVonId.ShouldBe(ApiFabrik.BenutzerId(Rolle.Benutzer));
        beleg.Dateien.ShouldContain(d => d.Rolle == DateiRolle.Original);
        _di.Aufrufe.ShouldBe(1);
    }

    [Fact]
    public async Task Als_Beleg_uebernehmen_nur_fuer_aussortierte_Vorgaenge()
    {
        var fertig = await _f.ImportiereAsync("rechnung.pdf", DiTestHilfen.Pdf, ImportQuelle.Mail, AusMail);
        var client = _f.ErzeugeClient(Rolle.Benutzer);

        (await client.PostAsync(AlsBeleg(fertig.Id), null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.PostAsync(AlsBeleg(Guid.NewGuid()), null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Als_Beleg_uebernehmen_scheitert_wenn_die_Datei_fehlt()
    {
        var aussortiert = await _f.ImportiereAsync("AGB.pdf", DiTestHilfen.Pdf, ImportQuelle.Mail, AusMail);
        File.Delete(aussortiert.StagingPfad!);

        var antwort = await _f.ErzeugeClient(Rolle.Benutzer).PostAsync(AlsBeleg(aussortiert.Id), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await _f.VorgangAsync(aussortiert.Id)).Status.ShouldBe(ImportStatus.Aussortiert);
    }

    [Fact]
    public async Task Als_Beleg_uebernehmen_braucht_Bearbeiten()
    {
        var aussortiert = await _f.ImportiereAsync("AGB.pdf", DiTestHilfen.Pdf, ImportQuelle.Mail, AusMail);

        var antwort = await _f.ErzeugeClient(Rolle.NurLesen).PostAsync(AlsBeleg(aussortiert.Id), null);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static string AlsBeleg(Guid id) => ApiRouten.Import.AlsBeleg.Replace("{id}", id.ToString());

    private async Task<List<Beleg>> BelegeAsync()
    {
        using var scope = _f.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Belege.AsNoTracking().Include(b => b.Dateien).ToListAsync();
    }

    /// <summary>PDF mit Textebene (Helvetica, nur ASCII), eine Zeile je Eintrag.</summary>
    private static byte[] PdfMitText(params string[] zeilen)
    {
        var builder = new PdfDocumentBuilder();
        var schrift = builder.AddStandard14Font(Standard14Font.Helvetica);
        var seite = builder.AddPage(PageSize.A4);
        var y = 800d;
        foreach (var zeile in zeilen)
        {
            seite.AddText(zeile, 11, new PdfPoint(40, y), schrift);
            y -= 16;
        }

        return builder.Build();
    }
}
