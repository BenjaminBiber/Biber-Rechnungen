using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class NeuerKorrespondentPruefungTests
{
    private readonly NeuerKorrespondentPruefung _pruefung = new();

    [Fact]
    public void Bekannter_Korrespondent_wie_im_Design()
    {
        _pruefung.Pruefen(PruefDaten.Eingabe()).ShouldBe(PruefBefundWert.Ok("Bekannt seit 03/2024 · 31 Belege"));
        _pruefung.Name.ShouldBe("Neuer Korrespondent");
    }

    [Fact]
    public void Ein_Beleg_in_der_Einzahl()
    {
        var eingabe = PruefDaten.Eingabe() with { KorrespondentBelege = 1 };

        _pruefung.Pruefen(eingabe).Detail.ShouldBe("Bekannt seit 03/2024 · 1 Beleg");
    }

    [Fact]
    public void Ungepruefter_Korrespondent_schlaegt_fehl()
    {
        var beleg = PruefDaten.Beleg();
        beleg.Korrespondent!.Geprueft = false;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).ShouldBe(PruefBefundWert.Fehler("Neuer Korrespondent"));
    }

    [Fact]
    public void Ohne_Korrespondent_nicht_anwendbar()
    {
        var beleg = PruefDaten.Beleg();
        beleg.KorrespondentId = null;
        beleg.Korrespondent = null;

        _pruefung.Pruefen(PruefDaten.Eingabe(beleg)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }
}
