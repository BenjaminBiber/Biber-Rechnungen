using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.FachlicheDublette;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class FachlicheDubletteTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Gleiche_drei_Merkmale_verknuepfen_mit_dem_Original()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var original = await BelegAnlegenAsync(fabrik, korrespondent, "2026-48817", 119.00m, tageAlt: 2);
        var neu = Neu(korrespondent, "2026-48817", 119.00m);

        (await AusfuehrenAsync(fabrik, neu)).ShouldBe(SchrittErgebnis.Weiter);

        neu.DuplikatVonId.ShouldBe(original.Id);
    }

    [Fact]
    public async Task Rechnungsnummer_wird_normalisiert_verglichen()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var original = await BelegAnlegenAsync(fabrik, korrespondent, "2026-48817", 80.50m, tageAlt: 2);
        var neu = Neu(korrespondent, "2026 48817", 80.50m);

        await AusfuehrenAsync(fabrik, neu);

        neu.DuplikatVonId.ShouldBe(original.Id);
    }

    [Fact]
    public async Task Anderer_Betrag_ergibt_keinen_Treffer()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        await BelegAnlegenAsync(fabrik, korrespondent, "2026-48817", 119.00m, tageAlt: 2);
        var neu = Neu(korrespondent, "2026-48817", 119.01m);

        await AusfuehrenAsync(fabrik, neu);

        neu.DuplikatVonId.ShouldBeNull();
    }

    [Fact]
    public async Task Anderer_Korrespondent_ergibt_keinen_Treffer()
    {
        var erster = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var zweiter = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        await BelegAnlegenAsync(fabrik, erster, "2026-48817", 119.00m, tageAlt: 2);
        var neu = Neu(zweiter, "2026-48817", 119.00m);

        await AusfuehrenAsync(fabrik, neu);

        neu.DuplikatVonId.ShouldBeNull();
    }

    [Fact]
    public async Task Fehlende_Rechnungsnummer_ergibt_keinen_Treffer()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var vorhanden = Testdaten.Beleg(korrespondentId: korrespondent.Id, brutto: 119.00m);
        vorhanden.Rechnungsnummer = null;
        vorhanden.ErstelltAm = fabrik.Zeit.GetUtcNow().AddDays(-2);
        await fabrik.SpeichernAsync(vorhanden);
        var neu = Neu(korrespondent, "2026-48817", 119.00m);
        neu.Rechnungsnummer = null;

        await AusfuehrenAsync(fabrik, neu);

        neu.DuplikatVonId.ShouldBeNull();
    }

    [Fact]
    public async Task Beleg_verknuepft_nie_auf_sich_selbst()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var beleg = await BelegAnlegenAsync(fabrik, korrespondent, "2026-48817", 119.00m, tageAlt: 0);

        await AusfuehrenAsync(fabrik, beleg);

        beleg.DuplikatVonId.ShouldBeNull();
    }

    [Fact]
    public async Task Eine_selbst_als_Dublette_markierte_Kopie_wird_nie_Ziel()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var wurzel = await BelegAnlegenAsync(fabrik, korrespondent, "2026-48817", 119.00m, tageAlt: 3);
        await BelegAnlegenAsync(fabrik, korrespondent, "2026-48817", 119.00m, tageAlt: 2, duplikatVonId: wurzel.Id);
        var neu = Neu(korrespondent, "2026-48817", 119.00m);

        await AusfuehrenAsync(fabrik, neu);

        neu.DuplikatVonId.ShouldBe(wurzel.Id);
    }

    [Fact]
    public async Task Bei_mehreren_Treffern_zaehlt_der_zuerst_erstellte()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var juenger = await BelegAnlegenAsync(fabrik, korrespondent, "2026-48817", 119.00m, tageAlt: 1);
        var aelter = await BelegAnlegenAsync(fabrik, korrespondent, "2026/48817", 119.00m, tageAlt: 5);
        var neu = Neu(korrespondent, "2026-48817", 119.00m);

        await AusfuehrenAsync(fabrik, neu);

        neu.DuplikatVonId.ShouldBe(aelter.Id);
        neu.DuplikatVonId.ShouldNotBe(juenger.Id);
    }

    [Fact]
    public async Task Nach_Korrektur_der_Rechnungsnummer_ist_die_Verknuepfung_weg()
    {
        var korrespondent = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var original = await BelegAnlegenAsync(fabrik, korrespondent, "2026-48817", 119.00m, tageAlt: 2);
        var neu = Neu(korrespondent, "2026-48817", 119.00m);
        await AusfuehrenAsync(fabrik, neu);
        neu.DuplikatVonId.ShouldBe(original.Id);

        neu.Rechnungsnummer = "2026-48818";
        await AusfuehrenAsync(fabrik, neu, istWiederholung: true);

        neu.DuplikatVonId.ShouldBeNull();
    }

    [Fact]
    public void Der_Schritt_hat_die_vorgesehene_Position_und_Bezeichnung()
    {
        using var scope = fabrik.ErzeugeScope();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<FachlicheDubletteSchritt>().Single();

        schritt.Reihenfolge.ShouldBe(SchrittReihenfolge.FachlicheDublette);
        schritt.Name.ShouldBe("Fachliche Dublette");
    }

    // Der neue Beleg ist jünger als alles, was bereits in der Datenbank liegt.
    private Beleg Neu(Korrespondent korrespondent, string rechnungsnummer, decimal brutto)
    {
        var beleg = Testdaten.Beleg(rechnungsnummer, brutto: brutto, korrespondentId: korrespondent.Id);
        beleg.ErstelltAm = fabrik.Zeit.GetUtcNow().AddDays(1);
        return beleg;
    }

    private static async Task<Beleg> BelegAnlegenAsync(
        ApiFabrik f,
        Korrespondent korrespondent,
        string rechnungsnummer,
        decimal brutto,
        int tageAlt,
        Guid? duplikatVonId = null)
    {
        var beleg = Testdaten.Beleg(rechnungsnummer, brutto: brutto, korrespondentId: korrespondent.Id);
        beleg.ErstelltAm = f.Zeit.GetUtcNow().AddDays(-tageAlt);
        beleg.DuplikatVonId = duplikatVonId;
        return await f.SpeichernAsync(beleg);
    }

    private static async Task<SchrittErgebnis> AusfuehrenAsync(ApiFabrik f, Beleg beleg, bool istWiederholung = false)
    {
        using var scope = f.ErzeugeScope();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<FachlicheDubletteSchritt>().Single();
        var vorgang = new ImportVorgang { Id = Guid.NewGuid(), Dateiname = "beleg.pdf", Status = ImportStatus.InVerarbeitung };
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = vorgang,
            Beleg = beleg,
            DateiPfad = Path.Combine(f.TempOrdner, "staging", "beleg.pdf"),
            Dateiname = vorgang.Dateiname,
            IstWiederholung = istWiederholung,
        };
        return await schritt.AusfuehrenAsync(kontext, CancellationToken.None);
    }
}
