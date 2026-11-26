using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Pipeline;

namespace Biber.Invoice.UnitTests.Pipeline;

public sealed class ExtraktionUebernehmenSchrittTests
{
    private const string Zahlungsbedingung = "Gesamt 2 478,43\nBitte zahlen Sie innerhalb von 14Tagen auf das angegebene Konto.";

    private static async Task<Beleg> UebernehmenAsync(ExtraktionsErgebnis extraktion)
    {
        var kontext = new VerarbeitungsKontext
        {
            Vorgang = new ImportVorgang { Id = Guid.NewGuid() },
            Beleg = new Beleg(),
            DateiPfad = "beleg.pdf",
            Dateiname = "beleg.pdf",
            Extraktion = extraktion,
        };

        await new ExtraktionUebernehmenSchritt(TimeProvider.System).AusfuehrenAsync(kontext, CancellationToken.None);
        return kontext.Beleg;
    }

    [Fact]
    public async Task Ohne_Faelligkeitsdatum_gilt_die_Frist_aus_dem_Volltext()
    {
        var beleg = await UebernehmenAsync(new ExtraktionsErgebnis
        {
            Belegart = Belegart.Rechnung,
            Rechnungsdatum = new DateOnly(2026, 10, 7),
            Volltext = Zahlungsbedingung,
        });

        beleg.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 10, 21));
    }

    [Fact]
    public async Task Ausgelesenes_Faelligkeitsdatum_hat_Vorrang()
    {
        var beleg = await UebernehmenAsync(new ExtraktionsErgebnis
        {
            Belegart = Belegart.Rechnung,
            Rechnungsdatum = new DateOnly(2026, 10, 7),
            Faelligkeitsdatum = new DateOnly(2026, 11, 1),
            Volltext = Zahlungsbedingung,
        });

        beleg.Faelligkeitsdatum.ShouldBe(new DateOnly(2026, 11, 1));
    }

    [Fact]
    public async Task Gutschrift_bekommt_keine_Frist_aus_dem_Volltext()
    {
        var beleg = await UebernehmenAsync(new ExtraktionsErgebnis
        {
            Belegart = Belegart.Gutschrift,
            Rechnungsdatum = new DateOnly(2026, 10, 7),
            Volltext = Zahlungsbedingung,
        });

        beleg.Faelligkeitsdatum.ShouldBeNull();
    }
}
