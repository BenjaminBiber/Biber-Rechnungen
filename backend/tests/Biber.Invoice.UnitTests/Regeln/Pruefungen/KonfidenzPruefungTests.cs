using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class KonfidenzPruefungTests
{
    private readonly KonfidenzPruefung _pruefung = new();

    [Fact]
    public void Unter_der_Schwelle_wie_im_Design()
    {
        _pruefung.Pruefen(MitKonfidenz(0.74m)).ShouldBe(PruefBefundWert.Fehler("Konfidenz 0,74 liegt unter der Schwelle 0,80"));
        _pruefung.Name.ShouldBe("Erkennungssicherheit");
    }

    [Fact]
    public void Genau_die_Schwelle_ist_ok()
    {
        _pruefung.Pruefen(MitKonfidenz(0.80m)).ShouldBe(PruefBefundWert.Ok("Konfidenz 0,80 erreicht die Schwelle 0,80"));
    }

    [Fact]
    public void Schwelle_kommt_aus_den_Einstellungen()
    {
        var eingabe = MitKonfidenz(0.74m) with { Einstellungen = new PruefEinstellungen { KonfidenzSchwelle = 0.7m } };

        _pruefung.Pruefen(eingabe).Ergebnis.ShouldBe(PruefErgebnis.Ok);
    }

    [Theory]
    [InlineData(ExtraktionsQuelle.XRechnung)]
    [InlineData(ExtraktionsQuelle.ZUGFeRD)]
    [InlineData(ExtraktionsQuelle.Manuell)]
    public void Ohne_Document_Intelligence_nicht_anwendbar(ExtraktionsQuelle quelle)
    {
        var beleg = PruefDaten.Beleg();
        beleg.ExtraktionsQuelle = quelle;
        beleg.DiKonfidenz = 0.1m;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }

    [Fact]
    public void Ohne_Konfidenzwert_nicht_anwendbar()
    {
        var beleg = PruefDaten.Beleg();
        beleg.DiKonfidenz = null;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }

    private static PruefEingabe MitKonfidenz(decimal konfidenz)
    {
        var beleg = PruefDaten.Beleg();
        beleg.DiKonfidenz = konfidenz;
        return PruefDaten.Eingabe(beleg);
    }
}
