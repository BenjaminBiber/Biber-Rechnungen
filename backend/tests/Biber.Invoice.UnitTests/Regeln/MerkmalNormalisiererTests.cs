using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Regeln;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class MerkmalNormalisiererTests
{
    [Theory]
    [InlineData("Größe", "Größe")]
    [InlineData("Gr.", "Größe")]
    [InlineData(" GRÖSSE ", "Größe")]
    [InlineData("size", "Größe")]
    [InlineData("Colour", "Farbe")]
    [InlineData("Paketgröße:", "Paketgröße")]
    [InlineData("Paketgröße::", "Paketgröße")]
    [InlineData("Gr./Farbe", "Größe/Farbe")]
    [InlineData("material", "Material")]
    [InlineData("Schuh  Weite", "Schuh Weite")]
    [InlineData(":", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Name_wird_vereinheitlicht(string? name, string? erwartet) =>
        MerkmalNormalisierer.Name(name).ShouldBe(erwartet);

    [Theory]
    [InlineData(" XL ", "XL")]
    [InlineData("9/10", "9/10")]
    [InlineData("schwarz \n XL", "schwarz XL")]
    [InlineData("-", null)]
    [InlineData(null, null)]
    public void Wert_wird_getrimmt(string? wert, string? erwartet) =>
        MerkmalNormalisierer.Wert(wert).ShouldBe(erwartet);

    [Fact]
    public void Bereinigen_behaelt_die_Reihenfolge_und_entfernt_Leere_und_Doppelte()
    {
        var merkmale = MerkmalNormalisierer.Bereinigen(
            [("Gr.", "42"), ("Farbe", " "), (null, "grau/marine/schwarz"), ("Größe", "42"), ("Paketgröße:", "1")]);

        merkmale.ShouldBe([new Merkmal("Größe", "42"), new Merkmal(null, "grau/marine/schwarz"), new Merkmal("Paketgröße", "1")]);
    }

    [Fact]
    public void Lange_Angaben_werden_gekuerzt_und_die_Anzahl_begrenzt()
    {
        MerkmalNormalisierer.Name(new string('n', 80))!.Length.ShouldBe(MerkmalNormalisierer.MaxName);
        MerkmalNormalisierer.Wert(new string('w', 150))!.Length.ShouldBe(MerkmalNormalisierer.MaxWert);
        MerkmalNormalisierer.Bereinigen(Enumerable.Range(1, 30).Select(i => ((string?)"Nr", (string?)i.ToString())))
            .Count.ShouldBe(MerkmalNormalisierer.MaxAnzahl);
    }

    [Fact]
    public void Als_Text_mit_und_ohne_Namen()
    {
        new Merkmal("Größe", "42").ToString().ShouldBe("Größe: 42");
        new Merkmal(null, "grau").ToString().ShouldBe("grau");
    }
}
