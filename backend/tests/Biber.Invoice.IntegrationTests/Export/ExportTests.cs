using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Export;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Export;

/// <summary>
/// Fester Datenbestand im ersten Halbjahr 2026 (Werte von Hand gerechnet):
/// <list type="bullet">
/// <item>B1 10.01. Alpha, Bürobedarf (Zeile 30), 19 %: 100,00 + 19,00 = 119,00 – mit Original</item>
/// <item>B2 15.02. Beta, Reisekosten (ohne EÜR-Zeile), 7 %: 200,00 + 14,00 = 214,00 – ZUGFeRD (PDF und XML)</item>
/// <item>B3 05.04. Alpha, Bürobedarf, 19 % 50,00 + 9,50 und 7 % 10,00 + 0,70 = 70,20</item>
/// <item>B4 01.05. Beta, ohne Kategorie, 19 %: 10,00 + 1,90 = 11,90 – „zu prüfen“</item>
/// <item>B5 Dublette von B1; B6 01.03. 50,00 USD (Reisekosten); B7 nicht fertig; B8 01.07. außerhalb</item>
/// </list>
/// </summary>
[Collection("Api")]
public sealed class ExportTests(ApiFabrik fabrik) : IAsyncLifetime
{
    private static readonly DateOnly Von = new(2026, 1, 1);
    private static readonly DateOnly Bis = new(2026, 6, 30);

    private Datenbestand _daten = null!;

    public async Task InitializeAsync()
    {
        fabrik.Zeit.Setze(new DateTimeOffset(2026, 7, 10, 8, 30, 0, TimeSpan.Zero));
        await fabrik.ZuruecksetzenAsync();
        _daten = await DatenbestandAnlegenAsync();

        // Einstellungen liegen im Speicher-Cache und überleben das Leeren der Datenbank.
        await UstSetzenAsync(UstStatus.Regelbesteuerung);
    }

    /// <summary>Setzt die USt-Einstellung zurück, damit sie nicht über den Cache in andere Testklassen gelangt.</summary>
    public async Task DisposeAsync()
    {
        using var scope = fabrik.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<IEinstellungen>()
            .SpeichernAsync(new UstEinstellungen(), CancellationToken.None);
    }

    [Fact]
    public async Task Vorschau_liefert_die_von_Hand_gerechneten_Zahlen()
    {
        var vorschau = await VorschauAsync(Anfrage());

        vorschau.AnzahlBelege.ShouldBe(5);
        vorschau.SummeBrutto.ShouldBe(415.10m);
        vorschau.Vorsteuer.ShouldBe(45.10m);
        vorschau.DublettenAusgeschlossen.ShouldBe(1);
        vorschau.AnzahlZuPruefen.ShouldBe(1);
        vorschau.ZipGroesse.ShouldBeNull();
        vorschau.Kategorien.Select(k => (k.Kategorie, k.EuerZeile, k.Brutto)).ShouldBe(
        [
            ("Bürobedarf", "Zeile 30", 189.20m),
            ("Reisekosten", "—", 214.00m),
            ("Ohne Kategorie", "—", 11.90m),
        ]);
    }

    [Fact]
    public async Task Vorschau_zaehlt_Dubletten_mit_und_schliesst_zu_pruefen_aus()
    {
        var mitDubletten = await VorschauAsync(Anfrage() with { DublettenAusschliessen = false });
        mitDubletten.AnzahlBelege.ShouldBe(6);
        mitDubletten.DublettenAusgeschlossen.ShouldBe(0);
        mitDubletten.SummeBrutto.ShouldBe(534.10m);

        var ohneZuPruefen = await VorschauAsync(Anfrage() with { ZuPruefenAusschliessen = true });
        ohneZuPruefen.AnzahlBelege.ShouldBe(4);
        ohneZuPruefen.AnzahlZuPruefen.ShouldBe(1);
        ohneZuPruefen.SummeBrutto.ShouldBe(403.20m);
    }

    [Fact]
    public async Task Vorschau_filtert_nach_Kategorie_Korrespondent_und_Tag()
    {
        (await VorschauAsync(Anfrage() with { KategorieIds = [_daten.Buero] })).AnzahlBelege.ShouldBe(2);
        (await VorschauAsync(Anfrage() with { KorrespondentIds = [_daten.Beta] })).AnzahlBelege.ShouldBe(3);
        (await VorschauAsync(Anfrage() with { TagIds = [SystemIds.TagZuPruefen] })).AnzahlBelege.ShouldBe(1);
    }

    [Fact]
    public async Task Vorschau_nennt_die_Groesse_der_Originale()
    {
        var vorschau = await VorschauAsync(Anfrage() with { ZipOriginale = true });

        vorschau.ZipGroesse.ShouldBe(_daten.Originale.Sum(o => (long)o.Inhalt.Length));
    }

    [Fact]
    public async Task Bei_Kleinunternehmer_ist_die_Vorsteuer_null_und_das_Blatt_fehlt()
    {
        await UstSetzenAsync(UstStatus.Kleinunternehmer);

        (await VorschauAsync(Anfrage())).Vorsteuer.ShouldBeNull();

        var lauf = await ErzeugenAsync(Anfrage() with { Belegliste = true, Vorsteuer = true, SummenJeKategorie = true });
        lauf.Inhalt.ShouldBe("Belegliste (xlsx), Summen");
        using var mappe = new XLWorkbook(new MemoryStream(await HerunterladenAsync(lauf)));
        mappe.Worksheets.Select(w => w.Name).ShouldBe(["Belegliste", "Summen je Kategorie"]);
    }

    [Fact]
    public async Task Belegliste_als_xlsx_hat_eine_Zeile_je_Beleg_Steuersatzspalten_und_Summenzeile()
    {
        var lauf = await ErzeugenAsync(Anfrage() with { Belegliste = true });
        lauf.Inhalt.ShouldBe("Belegliste (xlsx)");
        lauf.AnzahlBelege.ShouldBe(5);

        using var mappe = new XLWorkbook(new MemoryStream(await HerunterladenAsync(lauf)));
        var blatt = mappe.Worksheet("Belegliste");
        var kopf = blatt.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        kopf.ShouldBe(
        [
            "Rechnungsdatum", "Korrespondent", "Rechnungsnummer", "Titel", "Belegart", "Kategorie", "EÜR-Zeile",
            "Währung", "Netto 19 %", "USt 19 %", "Netto 7 %", "USt 7 %", "Netto gesamt", "USt gesamt", "Brutto",
            "Fälligkeit", "Tags", "Quelle", "Ablagepfad", "SHA-256",
        ]);
        kopf.ShouldNotContain(k => k.Contains("Zahl", StringComparison.OrdinalIgnoreCase));
        blatt.SheetView.SplitRow.ShouldBe(1);

        var spalte = kopf.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => x.i + 1);
        blatt.LastRowUsed()!.RowNumber().ShouldBe(7); // Kopf, 5 Belege, Summenzeile
        blatt.Column(spalte["Rechnungsnummer"]).Cells(2, 6).Select(c => c.GetString())
            .ShouldBe(["RE-1", "RE-2", "RE-6", "RE-3", "RE-4"]);

        var b3 = blatt.Row(5);
        b3.Cell(spalte["Rechnungsdatum"]).GetDateTime().ShouldBe(new DateTime(2026, 4, 5));
        b3.Cell(spalte["Rechnungsdatum"]).Style.DateFormat.Format.ShouldBe("dd.mm.yyyy");
        b3.Cell(spalte["Netto 19 %"]).GetValue<decimal>().ShouldBe(50.00m);
        b3.Cell(spalte["USt 7 %"]).GetValue<decimal>().ShouldBe(0.70m);
        b3.Cell(spalte["Brutto"]).GetValue<decimal>().ShouldBe(70.20m);
        b3.Cell(spalte["Brutto"]).Style.NumberFormat.Format.ShouldBe("#,##0.00");
        b3.Cell(spalte["EÜR-Zeile"]).GetString().ShouldBe("Zeile 30");

        var usd = blatt.Row(4);
        usd.Cell(spalte["Währung"]).GetString().ShouldBe("USD");
        usd.Cell(spalte["Brutto"]).GetValue<decimal>().ShouldBe(50.00m);

        var summe = blatt.Row(7);
        summe.Style.Font.Bold.ShouldBeTrue();
        summe.Cell(spalte["Netto 19 %"]).GetValue<decimal>().ShouldBe(160.00m);
        summe.Cell(spalte["USt 7 %"]).GetValue<decimal>().ShouldBe(14.70m);
        summe.Cell(spalte["Netto gesamt"]).GetValue<decimal>().ShouldBe(370.00m);
        summe.Cell(spalte["USt gesamt"]).GetValue<decimal>().ShouldBe(45.10m);
        summe.Cell(spalte["Brutto"]).GetValue<decimal>().ShouldBe(415.10m); // ohne die USD-Rechnung
    }

    [Fact]
    public async Task Belegliste_als_csv_mit_Semikolon_BOM_Dezimalkomma_und_deutschem_Datum()
    {
        var lauf = await ErzeugenAsync(Anfrage() with { Belegliste = true, BeleglisteFormat = "csv" });
        lauf.DownloadUrl.ShouldEndWith("/datei");

        using var zip = new ZipArchive(new MemoryStream(await HerunterladenAsync(lauf)));
        zip.Entries.Select(e => e.FullName).ShouldBe(["belegliste.csv"]);
        var bytes = Lesen(zip.GetEntry("belegliste.csv")!);
        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });

        var zeilen = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        zeilen.Length.ShouldBe(6); // Kopf und 5 Belege
        var kopf = zeilen[0].Split(';');
        kopf.ShouldContain("Netto 19 %");
        kopf.ShouldNotContain(k => k.Contains("Zahl", StringComparison.OrdinalIgnoreCase));
        var b3 = zeilen[4].Split(';');
        b3[Array.IndexOf(kopf, "Rechnungsdatum")].ShouldBe("05.04.2026");
        b3[Array.IndexOf(kopf, "Brutto")].ShouldBe("70,20");
        b3[Array.IndexOf(kopf, "USt 7 %")].ShouldBe("0,70");
    }

    [Fact]
    public async Task Summen_je_Kategorie_mit_EUeR_Zeile_und_Gesamtzeile()
    {
        var lauf = await ErzeugenAsync(Anfrage() with { SummenJeKategorie = true, BeleglisteFormat = "csv" });

        using var zip = new ZipArchive(new MemoryStream(await HerunterladenAsync(lauf)));
        var zeilen = CsvZeilen(zip, "summen-je-kategorie.csv");
        zeilen.ShouldBe(
        [
            "Kategorie;EÜR-Zeile;Anzahl;Netto;USt;Brutto",
            "Bürobedarf;Zeile 30;2;160,00;29,20;189,20",
            "Reisekosten;—;1;200,00;14,00;214,00",
            "Ohne Kategorie;—;1;10,00;1,90;11,90",
            "Gesamt;;4;370,00;45,10;415,10",
        ]);
    }

    [Fact]
    public async Task Vorsteuer_je_Quartal_und_Steuersatz()
    {
        var lauf = await ErzeugenAsync(Anfrage() with { Vorsteuer = true, BeleglisteFormat = "csv" });
        lauf.Inhalt.ShouldBe("Vorsteuer");

        using var zip = new ZipArchive(new MemoryStream(await HerunterladenAsync(lauf)));
        CsvZeilen(zip, "vorsteuer.csv").ShouldBe(
        [
            "Zeitraum;Steuersatz;Netto;Vorsteuer",
            "Q1 2026;19 %;100,00;19,00",
            "Q1 2026;7 %;200,00;14,00",
            "Q1 2026;Summe;300,00;33,00",
            "Q2 2026;19 %;60,00;11,40",
            "Q2 2026;7 %;10,00;0,70",
            "Q2 2026;Summe;70,00;12,10",
            "Gesamt;;370,00;45,10",
        ]);
    }

    [Fact]
    public async Task Vorsteuer_monatlich_laut_Einstellung()
    {
        await UstSetzenAsync(UstStatus.Regelbesteuerung, UstvaZeitraum.Monatlich);

        var lauf = await ErzeugenAsync(Anfrage() with { Vorsteuer = true, Bis = new DateOnly(2026, 2, 28) });

        using var mappe = new XLWorkbook(new MemoryStream(await HerunterladenAsync(lauf)));
        mappe.Worksheet("Vorsteuer").Column(1).CellsUsed().Select(c => c.GetString()).Distinct()
            .ShouldBe(["Zeitraum", "Januar 2026", "Februar 2026", "Gesamt"]);
    }

    [Fact]
    public async Task Zip_enthaelt_unveraenderte_Originale_und_stimmige_Pruefsummen()
    {
        var lauf = await ErzeugenAsync(Anfrage() with { Belegliste = true, ZipOriginale = true });
        lauf.Inhalt.ShouldBe("Belegliste (xlsx), ZIP");

        using var zip = new ZipArchive(new MemoryStream(await HerunterladenAsync(lauf)));
        zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal).ShouldBe(
            ["export.xlsx", "originale/2026/Alpha GmbH/2026-01-10_RE-1.pdf", "originale/2026/Beta AG/2026-02-15_RE-2.pdf",
                "originale/2026/Beta AG/2026-02-15_RE-2.xml", "pruefsummen.sha256"]);

        foreach (var original in _daten.Originale)
        {
            var inhalt = Lesen(zip.GetEntry($"originale/{original.Pfad}")!);
            inhalt.ShouldBe(original.Inhalt);
        }

        var pruefsummen = CsvText(zip.GetEntry("pruefsummen.sha256")!).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        pruefsummen.Length.ShouldBe(3);
        foreach (var zeile in pruefsummen)
        {
            var (hash, pfad) = (zeile[..64], zeile[66..]);
            zeile[64..66].ShouldBe("  ");
            Convert.ToHexStringLower(SHA256.HashData(Lesen(zip.GetEntry(pfad)!))).ShouldBe(hash);
        }
    }

    [Fact]
    public async Task Manipuliertes_Original_bricht_mit_409_und_Pfadliste_ab()
    {
        var manipuliert = _daten.Originale[0];
        var absolut = Path.Combine(fabrik.TempOrdner, "ablage", manipuliert.Pfad);
        await File.WriteAllBytesAsync(absolut, [.. manipuliert.Inhalt, 0x20]);

        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer)
            .PostJsonAsync(ApiRouten.Export.Erzeugen, Anfrage() with { ZipOriginale = true });

        antwort.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await antwort.Content.LesenAsync<JsonElement>();
        problem.GetProperty("pfade").EnumerateArray().Select(p => p.GetString()).ShouldBe([manipuliert.Pfad]);
        (await LaeufeAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Lauf_steht_in_der_Liste_ist_herunterladbar_und_im_Aenderungsprotokoll()
    {
        var lauf = await ErzeugenAsync(Anfrage() with { Belegliste = true, Bezeichnung = "Q1/Q2 2026: Steuerberater" });

        var liste = await LaeufeAsync();
        liste.ShouldHaveSingleItem().Id.ShouldBe(lauf.Id);
        liste[0].Bezeichnung.ShouldBe("Q1/Q2 2026: Steuerberater");

        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync(lauf.DownloadUrl);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("Export_Q1Q2_2026_Steuerberater_20260710-0830.xlsx");

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var eintrag = await db.AuditEintraege.SingleAsync(a => a.Entitaet == "Export");
        eintrag.Aktion.ShouldBe("Erstellt");
        eintrag.EntitaetId.ShouldBe(lauf.Id.ToString());
        eintrag.BenutzerId.ShouldBe(ApiFabrik.BenutzerId(Rolle.Benutzer));
        eintrag.NeuerWert.ShouldNotBeNull();
        eintrag.NeuerWert.ShouldContain("Zeitraum: 2026-01-01 – 2026-06-30");
        eintrag.NeuerWert.ShouldContain("Anzahl: 5");
    }

    [Fact]
    public async Task Fehlende_Datei_liefert_410()
    {
        var lauf = await ErzeugenAsync(Anfrage() with { Belegliste = true });
        foreach (var datei in Directory.EnumerateFiles(Path.Combine(fabrik.TempOrdner, "daten", "exporte")))
        {
            File.Delete(datei);
        }

        (await fabrik.ErzeugeClient().GetAsync(lauf.DownloadUrl)).StatusCode.ShouldBe(HttpStatusCode.Gone);
        (await fabrik.ErzeugeClient().GetAsync(ApiRouten.Export.LaufDatei.Replace("{id}", Guid.NewGuid().ToString(), StringComparison.Ordinal)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ungueltige_Anfragen_liefern_400()
    {
        var client = fabrik.ErzeugeClient(Rolle.Benutzer);

        (await client.PostJsonAsync(ApiRouten.Export.Erzeugen, Anfrage())).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostJsonAsync(ApiRouten.Export.Erzeugen, Anfrage() with { Belegliste = true, Von = Bis, Bis = Von }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostJsonAsync(ApiRouten.Export.Vorschau, Anfrage() with { Von = Bis, Bis = Von }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await UstSetzenAsync(UstStatus.Kleinunternehmer);
        (await client.PostJsonAsync(ApiRouten.Export.Erzeugen, Anfrage() with { Vorsteuer = true }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NurLesen_darf_nicht_erzeugen_aber_herunterladen()
    {
        var nurLesen = fabrik.ErzeugeClient(Rolle.NurLesen);

        (await nurLesen.PostJsonAsync(ApiRouten.Export.Erzeugen, Anfrage() with { Belegliste = true }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await nurLesen.PostJsonAsync(ApiRouten.Export.Vorschau, Anfrage())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var lauf = await ErzeugenAsync(Anfrage() with { Belegliste = true });
        (await nurLesen.GetAsync(ApiRouten.Export.Laeufe)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await nurLesen.GetAsync(lauf.DownloadUrl)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static ExportAnfrageDto Anfrage() => new(
        Von, Bis, "Halbjahr 2026",
        Belegliste: false, BeleglisteFormat: "xlsx", SummenJeKategorie: false, Vorsteuer: false, ZipOriginale: false,
        KategorieIds: [], KorrespondentIds: [], TagIds: [],
        DublettenAusschliessen: true, ZuPruefenAusschliessen: false);

    private async Task<ExportVorschauDto> VorschauAsync(ExportAnfrageDto anfrage)
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer).PostJsonAsync(ApiRouten.Export.Vorschau, anfrage);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await antwort.Content.LesenAsync<ExportVorschauDto>())!;
    }

    private async Task<ExportLaufDto> ErzeugenAsync(ExportAnfrageDto anfrage)
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.Benutzer).PostJsonAsync(ApiRouten.Export.Erzeugen, anfrage);
        antwort.StatusCode.ShouldBe(HttpStatusCode.Created, await antwort.Content.ReadAsStringAsync());
        return (await antwort.Content.LesenAsync<ExportLaufDto>())!;
    }

    private async Task<ExportLaufDto[]> LaeufeAsync() =>
        (await fabrik.ErzeugeClient(Rolle.NurLesen).GetJsonAsync<ExportLaufDto[]>(ApiRouten.Export.Laeufe))!;

    private async Task<byte[]> HerunterladenAsync(ExportLaufDto lauf)
    {
        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync(lauf.DownloadUrl);
        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await antwort.Content.ReadAsByteArrayAsync();
    }

    private async Task UstSetzenAsync(UstStatus status, UstvaZeitraum zeitraum = UstvaZeitraum.Quartalsweise)
    {
        using var scope = fabrik.ErzeugeScope();
        await scope.ServiceProvider.GetRequiredService<IEinstellungen>()
            .SpeichernAsync(new UstEinstellungen { Status = status, Zeitraum = zeitraum }, CancellationToken.None);
    }

    private static byte[] Lesen(ZipArchiveEntry eintrag)
    {
        using var quelle = eintrag.Open();
        using var puffer = new MemoryStream();
        quelle.CopyTo(puffer);
        return puffer.ToArray();
    }

    private static string CsvText(ZipArchiveEntry eintrag) => Encoding.UTF8.GetString(Lesen(eintrag)).TrimStart('﻿');

    private static string[] CsvZeilen(ZipArchive zip, string name) =>
        CsvText(zip.GetEntry(name)!).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    private sealed record Original(string Pfad, byte[] Inhalt);

    private sealed record Datenbestand(Guid Buero, Guid Beta, IReadOnlyList<Original> Originale);

    private async Task<Datenbestand> DatenbestandAnlegenAsync()
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        if (!await db.Tags.AnyAsync(t => t.Id == SystemIds.TagZuPruefen))
        {
            db.Tags.Add(new Tag { Id = SystemIds.TagZuPruefen, Name = "zu prüfen", Farbe = "orange", IstSystem = true });
        }

        var alpha = Testdaten.Korrespondent("Alpha GmbH");
        var beta = Testdaten.Korrespondent("Beta AG");
        var buero = Testdaten.Kategorie("Bürobedarf");
        buero.EuerZeile = "Zeile 30";
        var reise = Testdaten.Kategorie("Reisekosten");
        db.AddRange(alpha, beta, buero, reise);

        var originale = new List<Original>();
        var b1 = Beleg("RE-1", new DateOnly(2026, 1, 10), alpha, buero, (19, 100.00m, 19.00m));
        originale.Add(DateiAnlegen(b1, DateiRolle.Original, "2026/Alpha GmbH/2026-01-10_RE-1.pdf", "%PDF-1.7 RE-1"));
        var b2 = Beleg("RE-2", new DateOnly(2026, 2, 15), beta, reise, (7, 200.00m, 14.00m));
        originale.Add(DateiAnlegen(b2, DateiRolle.Original, "2026/Beta AG/2026-02-15_RE-2.pdf", "%PDF-1.7 RE-2 ZUGFeRD"));
        originale.Add(DateiAnlegen(b2, DateiRolle.EingebettetesXml, "2026/Beta AG/2026-02-15_RE-2.xml", "<rsm:CrossIndustryInvoice/>"));
        b2.Dateien.Add(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = b2.Id,
            Rolle = DateiRolle.Vorschau,
            Pfad = "vorschau/re-2.png",
            Sha256 = "x",
            Groesse = 999,
            MimeType = "image/png",
        });
        var b3 = Beleg("RE-3", new DateOnly(2026, 4, 5), alpha, buero, (19, 50.00m, 9.50m), (7, 10.00m, 0.70m));
        var b4 = Beleg("RE-4", new DateOnly(2026, 5, 1), beta, null, (19, 10.00m, 1.90m));
        b4.Tags.Add(new BelegTag { BelegId = b4.Id, TagId = SystemIds.TagZuPruefen });
        var b5 = Beleg("RE-1", new DateOnly(2026, 1, 10), alpha, buero, (19, 100.00m, 19.00m));
        b5.DuplikatVonId = b1.Id;
        var b6 = Beleg("RE-6", new DateOnly(2026, 3, 1), beta, reise);
        b6.Waehrung = "USD";
        b6.Netto = 50.00m;
        b6.Steuer = 0m;
        b6.Brutto = 50.00m;
        var b7 = Beleg("RE-7", new DateOnly(2026, 3, 1), alpha, buero, (19, 1000.00m, 190.00m));
        b7.Status = BelegStatus.InVerarbeitung;
        var b8 = Beleg("RE-8", new DateOnly(2026, 7, 1), alpha, buero, (19, 1000.00m, 190.00m));

        db.Belege.Add(b1);
        await db.SaveChangesAsync(); // B1 vor seiner Dublette
        db.Belege.AddRange(b2, b3, b4, b5, b6, b7, b8);
        await db.SaveChangesAsync();
        return new Datenbestand(buero.Id, beta.Id, originale);
    }

    private static Beleg Beleg(
        string nummer,
        DateOnly datum,
        Korrespondent korrespondent,
        Kategorie? kategorie,
        params (decimal Satz, decimal Netto, decimal Steuer)[] steuer)
    {
        var beleg = Testdaten.Beleg(nummer, datum, korrespondentId: korrespondent.Id, kategorieId: kategorie?.Id);
        beleg.Titel = $"Titel {nummer}";
        foreach (var (satz, netto, betrag) in steuer)
        {
            beleg.Steuerzeilen.Add(new Steuerzeile { Id = Guid.NewGuid(), BelegId = beleg.Id, Satz = satz, Netto = netto, Steuer = betrag });
        }

        beleg.Netto = steuer.Sum(s => s.Netto);
        beleg.Steuer = steuer.Sum(s => s.Steuer);
        beleg.Brutto = beleg.Netto + beleg.Steuer;
        return beleg;
    }

    private Original DateiAnlegen(Beleg beleg, DateiRolle rolle, string pfad, string text)
    {
        var inhalt = Encoding.UTF8.GetBytes(text);
        var absolut = Path.Combine(fabrik.TempOrdner, "ablage", pfad);
        Directory.CreateDirectory(Path.GetDirectoryName(absolut)!);
        File.WriteAllBytes(absolut, inhalt);
        beleg.Dateien.Add(new BelegDatei
        {
            Id = Guid.NewGuid(),
            BelegId = beleg.Id,
            Rolle = rolle,
            Pfad = pfad,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(inhalt)),
            Groesse = inhalt.Length,
            MimeType = rolle == DateiRolle.Original ? "application/pdf" : "application/xml",
            AbgelegtAm = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        });
        return new Original(pfad, inhalt);
    }
}
