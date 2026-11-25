using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.KorrespondentZuordnung;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Pipeline;

[Collection("Api")]
public sealed class KorrespondentZuordnungTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task UstIdNr_schlaegt_abweichenden_Namen_und_andere_Treffer()
    {
        var ust = UstIdNr();
        var name = $"Firma {Eindeutig()}";
        var nachUst = Testdaten.Korrespondent(ustIdNr: ust);
        nachUst.ErstelltAm = fabrik.Zeit.GetUtcNow().AddDays(1);
        await fabrik.SpeichernAsync(nachUst);
        await fabrik.SpeichernAsync(Testdaten.Korrespondent(name)); // gleicher Name, älter

        var (beleg, _) = await AusfuehrenAsync(Partei(name, ustIdNr: $" {ust[..2]} {ust[2..]} "));

        beleg.KorrespondentId.ShouldBe(nachUst.Id);
        beleg.KorrespondentErkennung.ShouldBe(KorrespondentErkennung.UstIdNr);
    }

    [Fact]
    public async Task Iban_trifft_vor_dem_Namen()
    {
        var iban = Iban();
        var name = $"Firma {Eindeutig()}";
        var nachIban = Testdaten.Korrespondent();
        nachIban.Bankverbindungen = [new Bankverbindung(iban)];
        await fabrik.SpeichernAsync(nachIban);
        await fabrik.SpeichernAsync(Testdaten.Korrespondent(name));

        var (beleg, _) = await AusfuehrenAsync(Partei(name, ibans: [Gruppiert(iban.ToLowerInvariant())]));

        beleg.KorrespondentId.ShouldBe(nachIban.Id);
        beleg.KorrespondentErkennung.ShouldBe(KorrespondentErkennung.Iban);
    }

    [Fact]
    public async Task Normalisierter_Name_trifft()
    {
        var kern = Eindeutig();
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent($"Bürowelt Krämer {kern} KG"));

        var (beleg, _) = await AusfuehrenAsync(Partei($"Buerowelt Kraemer {kern} GmbH"));

        beleg.KorrespondentId.ShouldBe(k.Id);
        beleg.KorrespondentErkennung.ShouldBe(KorrespondentErkennung.Name);
    }

    [Fact]
    public async Task Alias_trifft()
    {
        var alias = $"Alias {Eindeutig()}";
        var k = Testdaten.Korrespondent();
        k.Aliase = [alias];
        await fabrik.SpeichernAsync(k);

        var (beleg, _) = await AusfuehrenAsync(Partei(alias.ToUpperInvariant() + " AG"));

        beleg.KorrespondentId.ShouldBe(k.Id);
        beleg.KorrespondentErkennung.ShouldBe(KorrespondentErkennung.Name);
    }

    [Fact]
    public async Task Matching_Regel_trifft_den_aeltesten()
    {
        var wort = $"Kundennr{Eindeutig()}";
        var aelter = Testdaten.Korrespondent();
        aelter.Matching = new Matching { Algorithmus = MatchingAlgorithmus.EinWort, Muster = wort };
        aelter.ErstelltAm = fabrik.Zeit.GetUtcNow().AddDays(-5);
        var juenger = Testdaten.Korrespondent();
        juenger.ErstelltAm = fabrik.Zeit.GetUtcNow();
        juenger.Matching = new Matching { Algorithmus = MatchingAlgorithmus.Exakt, Muster = wort };
        var auto = Testdaten.Korrespondent();
        auto.Matching = new Matching { Algorithmus = MatchingAlgorithmus.Auto, Muster = wort };
        auto.ErstelltAm = fabrik.Zeit.GetUtcNow().AddDays(-10);
        await fabrik.SpeichernAsync(aelter);
        await fabrik.SpeichernAsync(juenger);
        await fabrik.SpeichernAsync(auto);

        var (beleg, _) = await AusfuehrenAsync(Partei($"Unbekannte Firma {Eindeutig()}"), volltext: $"Rechnung {wort} Danke");

        beleg.KorrespondentId.ShouldBe(aelter.Id);
        beleg.KorrespondentErkennung.ShouldBe(KorrespondentErkennung.Name);
    }

    [Fact]
    public async Task Ohne_Treffer_wird_ein_ungeprüfter_Lieferant_angelegt()
    {
        var name = $"Neue Firma {Eindeutig()} GmbH";
        var iban = Iban();
        var ust = UstIdNr();
        var partei = new ExtrahiertePartei(name, "Hauptstr. 1", "12345", "Berlin", "DE", ust, "12/345/67890", [new Bankverbindung(iban, "byladem1001")],
            "rechnung@neuefirma.example", "https://www.neuefirma.example/kontakt");

        var (beleg, _) = await AusfuehrenAsync(partei);

        var k = await KorrespondentAsync(beleg.KorrespondentId!.Value);
        k.Name.ShouldBe(name);
        k.Geprueft.ShouldBeFalse();
        k.IstLieferant.ShouldBeTrue();
        k.UstIdNr.ShouldBe(ust);
        k.Steuernummer.ShouldBe("12/345/67890");
        k.Bankverbindungen.ShouldBe([new Bankverbindung(iban, "BYLADEM1001")]);
        k.Anschrift.Ort.ShouldBe("Berlin");
        k.Anschrift.Plz.ShouldBe("12345");
        k.Email.ShouldBe("rechnung@neuefirma.example");
        k.Website.ShouldBe("https://www.neuefirma.example/kontakt");
        k.Domain.ShouldBe("neuefirma.example");
        k.ErstelltAm.ShouldBe(fabrik.Zeit.GetUtcNow());
    }

    [Fact]
    public async Task Ohne_Namen_heisst_der_neue_Korrespondent_nach_der_Rechnungsnummer()
    {
        var (beleg, _) = await AusfuehrenAsync(Partei(null, ustIdNr: UstIdNr()), rechnungsnummer: "RE-77");

        (await KorrespondentAsync(beleg.KorrespondentId!.Value)).Name.ShouldBe("Unbekannt RE-77");
    }

    [Fact]
    public async Task Beim_Treffer_bleiben_UstIdNr_und_IBANs_und_eine_abweichende_IBAN_wird_nicht_uebernommen()
    {
        var alteIban = Iban();
        var neueIban = Iban();
        var ust = UstIdNr();
        var k = Testdaten.Korrespondent(ustIdNr: ust);
        k.Bankverbindungen = [new Bankverbindung(alteIban)];
        await fabrik.SpeichernAsync(k);

        // Treffer über die alte IBAN; die Extraktion nennt eine andere USt-IdNr. und eine zweite IBAN.
        // Die zweite IBAN meldet die Prüfung „Bankverbindung“; übernommen wird sie erst beim Bestätigen.
        await AusfuehrenAsync(Partei(k.Name, ustIdNr: UstIdNr(), ibans: [alteIban, neueIban], steuernummer: "99/999/99999"));

        var gespeichert = await KorrespondentAsync(k.Id);
        gespeichert.UstIdNr.ShouldBe(ust);
        gespeichert.Bankverbindungen.ShouldBe([new Bankverbindung(alteIban)]);
        gespeichert.Steuernummer.ShouldBe("99/999/99999");
    }

    [Fact]
    public async Task Beim_Treffer_ohne_hinterlegte_IBAN_wird_die_erste_uebernommen()
    {
        var iban = Iban();
        var k = await fabrik.SpeichernAsync(Testdaten.Korrespondent());

        await AusfuehrenAsync(Partei(k.Name, ibans: [iban], bic: "COBADEFFXXX"));

        (await KorrespondentAsync(k.Id)).Bankverbindungen.ShouldBe([new Bankverbindung(iban, "COBADEFFXXX")]);
    }

    [Fact]
    public async Task Beim_Treffer_wird_fehlender_BIC_nachgetragen_und_vorhandener_bleibt()
    {
        var ohneBic = Iban();
        var mitBic = Iban();
        var k = Testdaten.Korrespondent();
        k.Bankverbindungen = [new Bankverbindung(ohneBic), new Bankverbindung(mitBic, "COBADEFFXXX")];
        await fabrik.SpeichernAsync(k);

        await AusfuehrenAsync(Partei(k.Name, ibans: [Gruppiert(ohneBic), mitBic], bic: "byladem1001"));

        (await KorrespondentAsync(k.Id)).Bankverbindungen
            .ShouldBe([new Bankverbindung(ohneBic, "BYLADEM1001"), new Bankverbindung(mitBic, "COBADEFFXXX")]);
    }

    [Fact]
    public async Task Unlesbarer_BIC_wird_nicht_uebernommen()
    {
        var iban = Iban();

        var (beleg, _) = await AusfuehrenAsync(Partei($"Firma {Eindeutig()}", ibans: [iban], bic: "Sparkasse Bonn"));

        (await KorrespondentAsync(beleg.KorrespondentId!.Value)).Bankverbindungen.ShouldBe([new Bankverbindung(iban)]);
    }

    [Fact]
    public async Task Manuell_gesetzter_Korrespondent_bleibt()
    {
        var manuell = await fabrik.SpeichernAsync(Testdaten.Korrespondent());
        var name = $"Firma {Eindeutig()}";
        await fabrik.SpeichernAsync(Testdaten.Korrespondent(name));
        var beleg = Testdaten.Beleg(korrespondentId: manuell.Id);
        beleg.KorrespondentErkennung = KorrespondentErkennung.Manuell;

        await AusfuehrenAsync(Partei(name), beleg: beleg);

        beleg.KorrespondentId.ShouldBe(manuell.Id);
        beleg.KorrespondentErkennung.ShouldBe(KorrespondentErkennung.Manuell);
    }

    [Fact]
    public async Task Ohne_Extraktion_oder_Angaben_bleibt_der_Korrespondent_leer()
    {
        var (ohneExtraktion, _) = await AusfuehrenAsync(null);
        var (ohneAngaben, _) = await AusfuehrenAsync(new ExtrahiertePartei(null, null, null, null, null, null, null, [], null, null));

        ohneExtraktion.KorrespondentId.ShouldBeNull();
        ohneAngaben.KorrespondentId.ShouldBeNull();
    }

    [Theory]
    [InlineData("Buchhaltung <rechnung@lieferant-{0}.example>", "lieferant-{0}.example")]
    [InlineData("jemand@gmail.com", null)]
    [InlineData("jemand@web.de", null)]
    public async Task Domain_kommt_aus_dem_Mail_Absender_ohne_Freemail(string absender, string? erwartet)
    {
        var kern = Eindeutig();
        var vorgang = new ImportVorgang { Id = Guid.NewGuid(), Dateiname = "x.pdf", MailAbsender = string.Format(absender, kern) };

        var (beleg, _) = await AusfuehrenAsync(Partei($"Firma {kern}"), vorgang: vorgang);

        (await KorrespondentAsync(beleg.KorrespondentId!.Value)).Domain.ShouldBe(erwartet is null ? null : string.Format(erwartet, kern));
    }

    [Fact]
    public async Task Zweiter_Lauf_legt_keinen_zweiten_Korrespondenten_an()
    {
        var name = $"Einmalig {Eindeutig()} GmbH";
        var partei = Partei(name);

        var (erster, _) = await AusfuehrenAsync(partei);
        var (zweiter, _) = await AusfuehrenAsync(partei, beleg: erster);

        zweiter.KorrespondentId.ShouldBe(erster.KorrespondentId);
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        (await db.Korrespondenten.CountAsync(k => k.Name == name)).ShouldBe(1);
    }

    private async Task<(Beleg Beleg, SchrittErgebnis Ergebnis)> AusfuehrenAsync(
        ExtrahiertePartei? aussteller,
        string? volltext = null,
        string? rechnungsnummer = null,
        Beleg? beleg = null,
        ImportVorgang? vorgang = null)
    {
        beleg ??= Testdaten.Beleg();
        vorgang ??= new ImportVorgang { Id = Guid.NewGuid(), Dateiname = "beleg.pdf" };
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var schritt = scope.ServiceProvider.GetServices<IPipelineSchritt>().OfType<KorrespondentZuordnungSchritt>().Single();
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = vorgang,
            Beleg = beleg,
            DateiPfad = "beleg.pdf",
            Dateiname = vorgang.Dateiname,
            Extraktion = aussteller is null && volltext is null
                ? null
                : new ExtraktionsErgebnis { Aussteller = aussteller, Volltext = volltext, Rechnungsnummer = rechnungsnummer },
        };

        var ergebnis = await schritt.AusfuehrenAsync(kontext, CancellationToken.None);
        await db.SaveChangesAsync();

        ergebnis.ShouldBe(SchrittErgebnis.Weiter);
        beleg.Korrespondent = null;
        return (beleg, ergebnis);
    }

    private async Task<Korrespondent> KorrespondentAsync(Guid id)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        return await db.Korrespondenten.AsNoTracking().SingleAsync(k => k.Id == id);
    }

    private static ExtrahiertePartei Partei(
        string? name, string? ustIdNr = null, IReadOnlyList<string>? ibans = null, string? steuernummer = null, string? bic = null) =>
        new(name, null, null, null, null, ustIdNr, steuernummer, [.. (ibans ?? []).Select(i => new Bankverbindung(i, bic))], null, null);

    private static string Eindeutig() => Guid.NewGuid().ToString("N")[..10];

    private static string UstIdNr() => "DE" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    private static string Iban() => "DE" + Random.Shared.Next(10, 99) + Random.Shared.NextInt64(100_000_000_000_000_000, 999_999_999_999_999_999);

    private static string Gruppiert(string iban) => string.Join(' ', iban.Chunk(4).Select(c => new string(c)));
}
