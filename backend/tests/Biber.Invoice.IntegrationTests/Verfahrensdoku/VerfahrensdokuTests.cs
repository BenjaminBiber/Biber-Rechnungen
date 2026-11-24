using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Verfahrensdoku;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using UglyToad.PdfPig;

namespace Biber.Invoice.IntegrationTests.Verfahrensdoku;

[Collection("Api")]
public sealed class VerfahrensdokuTests(ApiFabrik fabrik) : IDisposable
{
    private const string Beispiel = """
        # Beispiel-Verfahrensdokumentation

        Version: 2.3
        Stand: 2026-05-17

        ## Abschnitt Eins

        Ein **fetter** und ein *kursiver* Absatz mit `Code`.

        - Erster Punkt
        - Zweiter Punkt [ERGÄNZEN: Wert]

        | Rolle | Aufgabe |
        |---|---|
        | Verantwortlicher | Tabellenzellenwert [ERGÄNZEN] |

        ```
        codeblock zeile
        ```

        Noch eine Stelle: [ERGÄNZEN]
        """;

    private string Ordner => Path.Combine(fabrik.TempOrdner, "verfahrensdokumentation");

    private string Datei => Path.Combine(Ordner, VerfahrensdokuDienst.Dateiname);

    public void Dispose()
    {
        if (Directory.Exists(Ordner))
        {
            Directory.Delete(Ordner, recursive: true);
        }
    }

    [Fact]
    public async Task Stand_liefert_Version_Stand_und_offene_Stellen_aus_den_Kopfzeilen()
    {
        Schreiben(Beispiel);

        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync(ApiRouten.Verfahrensdoku.Info);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = (await antwort.Content.ReadFromJsonAsync<VerfahrensdokuDto>())!;
        dto.Version.ShouldBe("2.3");
        dto.Stand.ShouldBe(new DateOnly(2026, 5, 17));
        dto.OffeneStellen.ShouldBe(3);
        dto.SoftwareVersion.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ohne_Kopfzeilen_ist_die_Version_unbekannt_und_der_Stand_das_Aenderungsdatum()
    {
        Schreiben("# Nur ein Text\n\nOhne Angaben.");
        File.SetLastWriteTimeUtc(Datei, new DateTime(2025, 3, 9, 12, 0, 0, DateTimeKind.Utc));

        var dto = (await fabrik.ErzeugeClient(Rolle.NurLesen).GetFromJsonAsync<VerfahrensdokuDto>(ApiRouten.Verfahrensdoku.Info))!;

        dto.Version.ShouldBe("unbekannt");
        dto.Stand.ShouldBe(new DateOnly(2025, 3, 9));
        dto.OffeneStellen.ShouldBe(0);
    }

    [Fact]
    public async Task Pdf_beginnt_mit_PDF_hat_Seiten_und_enthaelt_Ueberschrift_und_Tabelleninhalt()
    {
        Schreiben(Beispiel);

        var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync(ApiRouten.Verfahrensdoku.Pdf);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        antwort.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        antwort.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        antwort.Content.Headers.ContentDisposition.FileName.ShouldBe("Verfahrensdokumentation.pdf");

        var bytes = await antwort.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).ShouldBe("%PDF");
        using var pdf = PdfDocument.Open(bytes);
        pdf.NumberOfPages.ShouldBeGreaterThanOrEqualTo(1);
        var text = string.Join(' ', pdf.GetPages().Select(s => s.Text));
        text.ShouldContain("Abschnitt Eins");
        text.ShouldContain("Tabellenzellenwert");
        text.ShouldContain("Verfahrensdokumentation Biber-Rechnungen");
        text.ShouldContain("Version 2.3");
    }

    [Fact]
    public async Task Fehlende_Datei_ergibt_404_mit_ProblemDetails()
    {
        foreach (var route in new[] { ApiRouten.Verfahrensdoku.Info, ApiRouten.Verfahrensdoku.Pdf })
        {
            var antwort = await fabrik.ErzeugeClient(Rolle.NurLesen).GetAsync(route);

            antwort.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            var inhalt = await antwort.Content.ReadAsStringAsync();
            inhalt.ShouldContain("Verfahrensdokumentation nicht gefunden");
        }
    }

    [Theory]
    [InlineData(ApiRouten.Verfahrensdoku.Info)]
    [InlineData(ApiRouten.Verfahrensdoku.Pdf)]
    public async Task Anonym_ergibt_401(string route)
    {
        Schreiben(Beispiel);

        var antwort = await fabrik.MitEchterAnmeldung().ErzeugeAnonymenClient().GetAsync(route);

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Die_echte_Verfahrensdokumentation_aus_dem_Repo_wird_zu_einem_lesbaren_PDF()
    {
        File.Copy(EchteDatei(), Schreiben(null), overwrite: true);

        var client = fabrik.ErzeugeClient(Rolle.NurLesen);
        var dto = (await client.GetFromJsonAsync<VerfahrensdokuDto>(ApiRouten.Verfahrensdoku.Info))!;
        dto.Version.ShouldNotBeNullOrWhiteSpace();
        dto.OffeneStellen.ShouldBeGreaterThan(0);

        var antwort = await client.GetAsync(ApiRouten.Verfahrensdoku.Pdf);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bytes = await antwort.Content.ReadAsByteArrayAsync();
        using var pdf = PdfDocument.Open(bytes);
        pdf.NumberOfPages.ShouldBeGreaterThan(1);
        var text = string.Join(' ', pdf.GetPages().Select(s => s.Text));
        text.ShouldContain("Verfahrensdokumentation");
        text.ShouldContain("Allgemeine Beschreibung");
        text.ShouldContain("Internes Kontrollsystem");
        text.ShouldContain("Azure Document Intelligence");
    }

    private string Schreiben(string? inhalt)
    {
        Directory.CreateDirectory(Ordner);
        if (inhalt is not null)
        {
            File.WriteAllText(Datei, inhalt);
        }

        return Datei;
    }

    private static string EchteDatei()
    {
        for (var ordner = new DirectoryInfo(AppContext.BaseDirectory); ordner is not null; ordner = ordner.Parent)
        {
            var kandidat = Path.Combine(ordner.FullName, "docs", "verfahrensdokumentation", VerfahrensdokuDienst.Dateiname);
            if (File.Exists(kandidat))
            {
                return kandidat;
            }
        }

        throw new FileNotFoundException("docs/verfahrensdokumentation/verfahrensdokumentation.md nicht gefunden.");
    }
}
