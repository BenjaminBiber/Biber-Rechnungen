using Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

namespace Biber.Invoice.UnitTests.Regeln.Pruefungen;

public sealed class DublettePruefungTests
{
    private readonly DublettePruefung _pruefung = new();

    [Fact]
    public void Ohne_Verknuepfung_kein_Treffer()
    {
        _pruefung.Pruefen(PruefDaten.Eingabe()).ShouldBe(PruefBefundWert.Ok("Kein Treffer"));
        _pruefung.Name.ShouldBe("Dublettenverdacht");
    }

    [Fact]
    public void Verknuepfte_Dublette_nennt_die_Rechnungsnummer()
    {
        var eingabe = PruefDaten.Eingabe() with { DuplikatVonNummer = "2026-48817" };

        _pruefung.Pruefen(eingabe).ShouldBe(PruefBefundWert.Fehler("Dublettenverdacht zu 2026-48817"));
    }
}
