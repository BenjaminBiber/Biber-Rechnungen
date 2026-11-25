using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class PruefLaufTests
{
    private static readonly IPruefung[] Alle =
    [
        new BankverbindungPruefung(),
        new KositPruefung(),
        new KonfidenzPruefung(),
        new NeuerKorrespondentPruefung(),
        new DublettePruefung(),
        new Paragraph14Pruefung(),
        new DatumPruefung(),
        new PflichtfelderPruefung(),
        new NettoSteuerBruttoPruefung(),
        new PositionssummePruefung(),
    ];

    [Fact]
    public void Alle_zehn_Pruefungen_in_fester_Reihenfolge()
    {
        var befunde = PruefLauf.Ausfuehren(PruefDaten.Eingabe(), Alle);

        befunde.Select(b => b.Art).ShouldBe(Enum.GetValues<PruefungArt>());
        befunde.ShouldNotContain(b => b.Wert.Ergebnis == PruefErgebnis.Fehlgeschlagen);
    }

    [Fact]
    public void Abgeschaltete_Pruefung_erzeugt_keinen_Befund()
    {
        var einstellungen = new PruefEinstellungen
        {
            Aktiv = Enum.GetValues<PruefungArt>().ToDictionary(a => a, a => a != PruefungArt.Konfidenz),
        };
        var beleg = PruefDaten.Beleg();
        beleg.DiKonfidenz = 0.1m;

        var befunde = PruefLauf.Ausfuehren(PruefDaten.Eingabe(beleg, einstellungen), Alle);

        befunde.Count.ShouldBe(9);
        befunde.ShouldNotContain(b => b.Art == PruefungArt.Konfidenz);
    }

    [Fact]
    public void Fehlender_Eintrag_in_Aktiv_gilt_als_eingeschaltet()
    {
        var befunde = PruefLauf.Ausfuehren(PruefDaten.Eingabe(einstellungen: new PruefEinstellungen { Aktiv = new Dictionary<PruefungArt, bool>() }), Alle);

        befunde.Count.ShouldBe(10);
    }
}
