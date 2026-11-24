using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class NettoSteuerBruttoPruefungTests
{
    private readonly NettoSteuerBruttoPruefung _pruefung = new();

    [Fact]
    public void Ein_Steuersatz_wie_im_Design()
    {
        _pruefung.Pruefen(PruefDaten.Eingabe()).ShouldBe(PruefBefundWert.Ok("19 %: 221,85 € + 42,15 € = 264,00 €"));
        _pruefung.Name.ShouldBe("Netto + USt = Brutto je Steuersatz");
    }

    [Fact]
    public void Zwei_Steuersaetze_werden_einzeln_und_in_Summe_geprueft()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Steuerzeilen =
        [
            new Steuerzeile { Satz = 7m, Netto = 50m, Steuer = 3.50m },
            new Steuerzeile { Satz = 19m, Netto = 100m, Steuer = 19m },
        ];
        beleg.Brutto = 172.50m;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg))
            .ShouldBe(PruefBefundWert.Ok("19 %: 100,00 € + 19,00 € = 119,00 € · 7 %: 50,00 € + 3,50 € = 53,50 €"));
    }

    [Fact]
    public void Falsche_Steuer_und_falsches_Brutto_werden_genannt()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Steuerzeilen = [new Steuerzeile { Satz = 19m, Netto = 221.85m, Steuer = 40m }];

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Fehler(
            "19 %: Steuer 40,00 € statt 42,15 € · Netto + USt 261,85 € statt Brutto 264,00 €"));
    }

    [Theory]
    [InlineData("264.02", PruefErgebnis.Ok)]
    [InlineData("264.03", PruefErgebnis.Fehlgeschlagen)]
    public void Brutto_mit_Toleranz(string brutto, PruefErgebnis erwartet)
    {
        var beleg = PruefDaten.Beleg();
        beleg.Brutto = decimal.Parse(brutto, System.Globalization.CultureInfo.InvariantCulture);

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).Ergebnis.ShouldBe(erwartet);
    }

    [Fact]
    public void Ohne_Steuerzeilen_nicht_anwendbar()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Steuerzeilen = [];

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }
}
