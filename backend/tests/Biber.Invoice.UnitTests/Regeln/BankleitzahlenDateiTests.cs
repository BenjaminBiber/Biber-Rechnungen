using System.Text;
using Biber.Invoice.Infrastructure.Module.Banken;

namespace Biber.Invoice.UnitTests.Regeln;

public sealed class BankleitzahlenDateiTests
{
    [Fact]
    public void Liest_Hauptstellen_ohne_Filialen_und_ohne_Zusatz_alt()
    {
        var datei = Datei(
            Satz("73369264", '1', "Raiffeisenbank im Allgäuer Land"),
            Satz("73369264", '2', "Raiffeisenbank im Allgäuer Land"),
            Satz("72152070", '1', "Sparkasse Neuburg-Rain -alt-"),
            Satz("65092200", '1', "Volksbank Altshausen -alt-."),
            Satz("40030000", '1', "Münsterländische Bank Thie,Zndl. d.VR-Bank Westmünst.-alt-"));

        var banken = BankleitzahlenDatei.Lesen(datei).ToDictionary(b => b.Blz, b => b.Name);

        banken.Count.ShouldBe(BankleitzahlenDatei.MindestAnzahl + 4);
        banken["73369264"].ShouldBe("Raiffeisenbank im Allgäuer Land");
        banken["72152070"].ShouldBe("Sparkasse Neuburg-Rain");
        banken["65092200"].ShouldBe("Volksbank Altshausen");
        banken["40030000"].ShouldBe("Münsterländische Bank Thie,Zndl. d.VR-Bank Westmünst.");
    }

    [Fact]
    public void Liest_auch_eine_als_UTF8_gespeicherte_Datei()
    {
        var datei = Datei(Encoding.UTF8, Satz("73369264", '1', "Raiffeisenbank im Allgäuer Land"));

        BankleitzahlenDatei.Lesen(datei).Single(b => b.Blz == "73369264").Name.ShouldBe("Raiffeisenbank im Allgäuer Land");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bankleitzahl;Merkmal;Bezeichnung;PLZ;Ort\n10000000;1;Bundesbank;10591;Berlin")]
    [InlineData("%PDF-1.7")]
    public void Andere_Dateien_werden_abgelehnt(string inhalt)
    {
        Should.Throw<BankleitzahlenAusnahme>(() => BankleitzahlenDatei.Lesen(Encoding.Latin1.GetBytes(inhalt)))
            .Message.ShouldContain("keine Bankleitzahlendatei");
    }

    [Fact]
    public void Unvollstaendige_Datei_wird_abgelehnt()
    {
        var inhalt = Encoding.Latin1.GetBytes(Satz("37040044", '1', "Commerzbank") + "\r\n");

        Should.Throw<BankleitzahlenAusnahme>(() => BankleitzahlenDatei.Lesen(inhalt))
            .Message.ShouldContain("nur 1 Banken");
    }

    /// <summary>Datei im Format der Bundesbank: <see cref="BankleitzahlenDatei.MindestAnzahl"/> Füllbanken plus <paramref name="saetze"/>.</summary>
    public static byte[] Datei(params string[] saetze) => Datei(Encoding.Latin1, saetze);

    private static byte[] Datei(Encoding kodierung, params string[] saetze)
    {
        var fueller = Enumerable.Range(0, BankleitzahlenDatei.MindestAnzahl)
            .Select(i => Satz($"1{i:D7}", '1', $"Testbank {i}"));
        return kodierung.GetBytes(string.Join("\r\n", fueller.Concat(saetze)) + "\r\n");
    }

    /// <summary>Ein Satz mit 168 Zeichen: BLZ, Merkmal, Bezeichnung (58), PLZ, Ort, Kurzbezeichnung, PAN, BIC, …</summary>
    public static string Satz(string blz, char merkmal, string name) =>
        blz + merkmal + name.PadRight(58) + "10115" + "Berlin".PadRight(35) + name.PadRight(27)[..27]
        + "     " + "TESTDEFFXXX" + "00" + "000001" + "U" + "0" + "00000000";
}
