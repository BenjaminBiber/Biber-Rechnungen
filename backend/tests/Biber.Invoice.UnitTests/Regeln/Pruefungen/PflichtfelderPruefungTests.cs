using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class PflichtfelderPruefungTests
{
    private readonly PflichtfelderPruefung _pruefung = new();

    [Fact]
    public void Alle_Felder_vorhanden()
    {
        _pruefung.Pruefen(PruefDaten.Eingabe())
            .ShouldBe(PruefBefundWert.Ok("Korrespondent, Datum, Rechnungsnummer und Gesamtbetrag vorhanden"));
    }

    [Fact]
    public void Ein_fehlendes_Feld()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Rechnungsnummer = " ";

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Fehler("Rechnungsnummer fehlt"));
    }

    [Fact]
    public void Alle_Felder_fehlen()
    {
        var beleg = PruefDaten.Beleg();
        beleg.KorrespondentId = null;
        beleg.Korrespondent = null;
        beleg.Rechnungsdatum = null;
        beleg.Rechnungsnummer = null;
        beleg.Brutto = null;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg))
            .ShouldBe(PruefBefundWert.Fehler("Korrespondent, Datum, Rechnungsnummer und Gesamtbetrag fehlen"));
    }
}
