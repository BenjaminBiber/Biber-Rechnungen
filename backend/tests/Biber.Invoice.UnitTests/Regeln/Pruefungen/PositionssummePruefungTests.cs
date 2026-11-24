using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class PositionssummePruefungTests
{
    private readonly PositionssummePruefung _pruefung = new();

    [Fact]
    public void Summe_gleich_Netto_ist_ok()
    {
        var wert = _pruefung.Pruefen(PruefDaten.Eingabe());

        wert.ShouldBe(PruefBefundWert.Ok("Positionen 221,85 € = Netto 221,85 €"));
        _pruefung.Name.ShouldBe("Summe der Positionen = Netto");
    }

    [Theory]
    [InlineData("221.83", PruefErgebnis.Ok)]
    [InlineData("221.87", PruefErgebnis.Ok)]
    [InlineData("221.82", PruefErgebnis.Fehlgeschlagen)]
    [InlineData("221.88", PruefErgebnis.Fehlgeschlagen)]
    public void Toleranz_genau_zwei_Cent_ist_noch_ok(string netto, PruefErgebnis erwartet)
    {
        var beleg = PruefDaten.Beleg();
        beleg.Netto = decimal.Parse(netto, System.Globalization.CultureInfo.InvariantCulture);

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).Ergebnis.ShouldBe(erwartet);
    }

    [Fact]
    public void Abweichung_nennt_Summe_Soll_Differenz_und_Toleranz_wie_im_Design()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Positionen[1].Gesamt = 102.15m;

        var wert = _pruefung.Pruefen(PruefDaten.Eingabe(beleg));

        wert.ShouldBe(PruefBefundWert.Fehler("Positionen 219,85 € statt 221,85 € · Differenz 2,00 €, Toleranz 0,02 €"));
    }

    [Fact]
    public void Positionen_mit_Bruttopreisen_passen_zum_Brutto()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Positionen = [PruefDaten.Position(1, 200m), PruefDaten.Position(2, 64m)];

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Ok("Positionen 264,00 € = Brutto 264,00 €"));
    }

    [Fact]
    public void Ohne_Netto_wird_gegen_das_Brutto_verglichen()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Netto = null;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).Detail.ShouldBe("Positionen 221,85 € statt 264,00 € · Differenz 42,15 €, Toleranz 0,02 €");
    }

    [Fact]
    public void Toleranz_kommt_aus_den_Einstellungen()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Netto = 222.85m;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg, new PruefEinstellungen { SummenToleranz = 1m })).Ergebnis.ShouldBe(PruefErgebnis.Ok);
    }

    [Fact]
    public void Ohne_Positionen_oder_Betraege_nicht_anwendbar()
    {
        var ohnePositionen = PruefDaten.Beleg();
        ohnePositionen.Positionen = [PruefDaten.Position(1, null)];
        var ohneBetraege = PruefDaten.Beleg();
        ohneBetraege.Netto = null;
        ohneBetraege.Brutto = null;

        _pruefung.Pruefen(PruefDaten.Eingabe(ohnePositionen)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
        _pruefung.Pruefen(PruefDaten.Eingabe(ohneBetraege)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }
}
