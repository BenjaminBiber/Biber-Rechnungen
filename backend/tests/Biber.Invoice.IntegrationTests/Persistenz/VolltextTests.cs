using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.Infrastructure.Persistenz.Konfiguration;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;

namespace Biber.Invoice.IntegrationTests.Persistenz;

[Collection("Api")]
public sealed class VolltextTests(ApiFabrik fabrik)
{
    [Fact]
    public async Task Suchvektor_findet_Wort_im_Volltext_und_in_der_Rechnungsnummer()
    {
        var imVolltext = await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Lieferung: 2 Stück Toner schwarz für Drucker"));
        var inNummer = await fabrik.SpeichernAsync(Testdaten.Beleg(rechnungsnummer: "TONER-4711"));
        var ohne = await fabrik.SpeichernAsync(Testdaten.Beleg(volltext: "Papier A4, 500 Blatt"));
        Guid[] ids = [imVolltext.Id, inNummer.Id, ohne.Id];

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var treffer = await db.Belege
            .Where(b => ids.Contains(b.Id))
            .Where(b => EF.Property<NpgsqlTsVector>(b, BelegKonfiguration.Suchvektor)
                .Matches(EF.Functions.PlainToTsQuery("german", "Toner")))
            .Select(b => b.Id)
            .ToListAsync();

        treffer.ShouldBe([imVolltext.Id, inNummer.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Trigram_Suche_findet_Kraemer_fuer_Kraemer_KG()
    {
        var kraemer = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Krämer KG"));
        var anderer = await fabrik.SpeichernAsync(Testdaten.Korrespondent("Bürobedarf Schulze GmbH"));
        Guid[] ids = [kraemer.Id, anderer.Id];

        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        var treffer = await db.Korrespondenten
            // similarity('Krämer KG', 'Kraemer') ≈ 0,31 liegt über der Standardschwelle 0,3 des Operators %.
            .FromSql($"SELECT * FROM korrespondent WHERE name % {"Kraemer"}")
            .Where(k => ids.Contains(k.Id))
            .Select(k => k.Name)
            .ToListAsync();

        treffer.ShouldBe(["Krämer KG"]);
    }
}
