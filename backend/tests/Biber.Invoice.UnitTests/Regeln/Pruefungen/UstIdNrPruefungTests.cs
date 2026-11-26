using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class UstIdNrPruefungTests
{
    private readonly UstIdNrPruefung _pruefung = new();

    [Fact]
    public void Gueltige_Nummer_ist_ok()
    {
        var eingabe = Eingabe(UstIdStatus.Gueltig);

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.Ok("USt-IdNr. DE284117560 ist laut VIES gültig (geprüft am 20.11.2026)"));
        _pruefung.Name.ShouldBe("USt-IdNr. gültig (VIES)");
    }

    [Fact]
    public void Ungueltige_Nummer_schlaegt_fehl()
    {
        _pruefung.Pruefen(Eingabe(UstIdStatus.Ungueltig)).Ergebnis.ShouldBe(PruefErgebnis.Fehlgeschlagen);
    }

    [Fact]
    public void Ergebnis_fuer_eine_andere_Nummer_gilt_nicht()
    {
        var eingabe = Eingabe(UstIdStatus.Ungueltig, geprueftNummer: "DE111111111");

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.NichtAnwendbar("Die USt-IdNr. wurde noch nicht online geprüft"));
    }

    [Theory]
    [InlineData(UstIdStatus.NichtErreichbar)]
    [InlineData(UstIdStatus.NichtPruefbar)]
    [InlineData(UstIdStatus.Ungeprueft)]
    public void Ohne_Ergebnis_nicht_anwendbar(UstIdStatus status)
    {
        _pruefung.Pruefen(Eingabe(status)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }

    [Fact]
    public void Ohne_USt_IdNr_nicht_anwendbar()
    {
        var b = PruefDaten.Beleg();
        b.Korrespondent!.UstIdNr = null;

        _pruefung.Pruefen(PruefDaten.Eingabe(b)).Ergebnis.ShouldBe(PruefErgebnis.NichtAnwendbar);
    }

    private static PruefEingabe Eingabe(UstIdStatus status, string? geprueftNummer = null)
    {
        var b = PruefDaten.Beleg();
        var k = b.Korrespondent!;
        k.UstIdStatus = status;
        k.UstIdGeprueftNummer = geprueftNummer ?? k.UstIdNr;
        k.UstIdGeprueftAm = new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
        return PruefDaten.Eingabe(b);
    }
}
