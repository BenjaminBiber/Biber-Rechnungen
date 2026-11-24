using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class DatumPruefungTests
{
    private readonly DatumPruefung _pruefung = new();

    [Fact]
    public void Datum_wie_im_Design()
    {
        _pruefung.Pruefen(PruefDaten.Eingabe())
            .ShouldBe(PruefBefundWert.Ok("19.11.2026 – nicht in der Zukunft, jünger als 2 Jahre"));
    }

    [Fact]
    public void Heute_ist_ok()
    {
        _pruefung.Pruefen(MitDatum(PruefDaten.Heute)).Ergebnis.ShouldBe(PruefErgebnis.Ok);
    }

    [Fact]
    public void Morgen_liegt_in_der_Zukunft()
    {
        _pruefung.Pruefen(MitDatum(PruefDaten.Heute.AddDays(1)))
            .ShouldBe(PruefBefundWert.Fehler("25.11.2026 liegt in der Zukunft"));
    }

    [Fact]
    public void Exakt_zwei_Jahre_alt_ist_noch_ok_einen_Tag_mehr_nicht()
    {
        _pruefung.Pruefen(MitDatum(PruefDaten.Heute.AddYears(-2))).Ergebnis.ShouldBe(PruefErgebnis.Ok);
        _pruefung.Pruefen(MitDatum(PruefDaten.Heute.AddYears(-2).AddDays(-1)))
            .ShouldBe(PruefBefundWert.Fehler("23.11.2024 ist älter als 2 Jahre"));
    }

    [Fact]
    public void Hoechstalter_kommt_aus_den_Einstellungen()
    {
        var eingabe = MitDatum(PruefDaten.Heute.AddMonths(-13)) with { Einstellungen = new PruefEinstellungen { MaxAlterJahre = 1 } };

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.Fehler("24.10.2025 ist älter als 1 Jahr"));
    }

    [Fact]
    public void Ohne_Datum_nicht_anwendbar()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Rechnungsdatum = null;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }

    private static PruefEingabe MitDatum(DateOnly datum)
    {
        var beleg = PruefDaten.Beleg();
        beleg.Rechnungsdatum = datum;
        return PruefDaten.Eingabe(beleg);
    }
}
