using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class KositPruefungTests
{
    private readonly KositPruefung _pruefung = new();

    [Fact]
    public void Gescanntes_PDF_wie_im_Design()
    {
        _pruefung.Pruefen(PruefDaten.Eingabe()).ShouldBe(PruefBefundWert.NichtAnwendbar("Keine E-Rechnung (gescanntes PDF)"));
        _pruefung.Name.ShouldBe("KoSIT-Validierung");
    }

    [Fact]
    public void Gueltige_E_Rechnung_ist_ok()
    {
        _pruefung.Pruefen(ERechnung(new KositErgebnis(true, []))).ShouldBe(PruefBefundWert.Ok("E-Rechnung gültig"));
    }

    [Fact]
    public void Eine_Meldung()
    {
        _pruefung.Pruefen(ERechnung(new KositErgebnis(false, ["BR-DE-15 Käuferreferenz fehlt"])))
            .ShouldBe(PruefBefundWert.Fehler("KoSIT: BR-DE-15 Käuferreferenz fehlt"));
    }

    [Fact]
    public void Mehrere_Meldungen_werden_verbunden()
    {
        _pruefung.Pruefen(ERechnung(new KositErgebnis(false, ["BR-DE-15 Käuferreferenz fehlt", " ", "BR-CO-10 Summe falsch "]), ExtraktionsQuelle.ZUGFeRD))
            .Detail.ShouldBe("KoSIT: BR-DE-15 Käuferreferenz fehlt · BR-CO-10 Summe falsch");
    }

    [Fact]
    public void Ungueltig_ohne_Meldung()
    {
        _pruefung.Pruefen(ERechnung(new KositErgebnis(false, []))).Detail.ShouldBe("KoSIT: E-Rechnung ungültig");
    }

    [Fact]
    public void E_Rechnung_ohne_Validierung_nicht_anwendbar()
    {
        _pruefung.Pruefen(ERechnung(null)).ShouldBe(PruefBefundWert.NichtAnwendbar("Nicht validiert"));
    }

    private static PruefEingabe ERechnung(KositErgebnis? kosit, ExtraktionsQuelle quelle = ExtraktionsQuelle.XRechnung)
    {
        var beleg = PruefDaten.Beleg();
        beleg.ExtraktionsQuelle = quelle;
        return PruefDaten.Eingabe(beleg) with { Kosit = kosit };
    }
}
