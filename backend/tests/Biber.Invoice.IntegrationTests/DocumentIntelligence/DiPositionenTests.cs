using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using Azure.AI.DocumentIntelligence;
using Biber.Invoice.Domain.Pipeline;
using DiClient = Biber.Invoice.Infrastructure.Module.DocumentIntelligence.DocumentIntelligenceClient;

namespace Biber.Invoice.IntegrationTests.DocumentIntelligence;

/// <summary>
/// Nachbearbeitung der Positionen aus <c>prebuilt-invoice</c>. Die Items sind der echten Antwort für eine
/// VetStar-Tierarztrechnung nachgebildet (Menge nur im Text, Fortsetzungstabelle auf Seite 2).
/// </summary>
public sealed class DiPositionenTests
{
    [Fact]
    public void Verschmolzene_Zeilen_einer_Folgeseite_werden_aufgeteilt()
    {
        var e = Abbilden(Item(
            "2 x 25,0 Dos NASYM PET 25 Dos.\n306,36\n28.10.2026\n100,0 ml Buscopan compositum\n38,22\n-",
            "2 x 25,0 Dos NASYM PET 25 Dos.\n100,0 ml Buscopan compositum",
            306.36));

        e.Positionen.ShouldBe(
        [
            new ExtrahiertePosition(1, "25,0 Dos NASYM PET 25 Dos.", 2m, null, 153.18m, 306.36m, null),
            new ExtrahiertePosition(2, "100,0 ml Buscopan compositum", null, null, null, 38.22m, null),
        ]);
    }

    [Fact]
    public void Ein_Item_mit_Datum_und_einem_Betrag_bleibt_eine_Position()
    {
        var e = Abbilden(Item("16.10.2026\nRind Euthanasie\n35,94\n-", "Rind Euthanasie", 35.94));

        e.Positionen.ShouldBe([new ExtrahiertePosition(1, "Rind Euthanasie", null, null, null, 35.94m, null)]);
    }

    [Fact]
    public void Passt_der_DI_Betrag_nicht_zur_ersten_Teilposition_bleibt_das_Item_ganz()
    {
        var e = Abbilden(Item("Wartung\n50,00\nAnfahrt\n20,00", "Wartung Anfahrt", 70.00));

        e.Positionen.ShouldHaveSingleItem().Gesamt.ShouldBe(70.00m);
    }

    [Theory]
    [InlineData("3 x 1,0 Dos Engemycin Spray, 25 mg/ml", 56.34, 3, "1,0 Dos Engemycin Spray, 25 mg/ml", 18.78)]
    [InlineData("2 × 100,0 ml Eficur", 147.62, 2, "100,0 ml Eficur", 73.81)]
    [InlineData("14 x Trächtigkeitsuntersuchung", 107.80, 14, "Trächtigkeitsuntersuchung", 7.70)]
    [InlineData("7x 5,0 Dos NASYM PET 5 Dos.", 248.85, 7, "5,0 Dos NASYM PET 5 Dos.", 35.55)]
    public void Menge_vorn_im_Text_wird_erkannt(string text, double gesamt, int menge, string rest, double einzelpreis)
    {
        var e = Abbilden(Item($"{text}\n{gesamt:0.00}", text, gesamt));

        e.Positionen.ShouldBe([new ExtrahiertePosition(1, rest, menge, null, (decimal)einzelpreis, (decimal)gesamt, null)]);
    }

    [Theory]
    [InlineData("3,00 ml Xylazin 20 mg/ml")]
    [InlineData("500,0 g Natriumbicarbonat")]
    [InlineData("Kuh Stoffwechselstörung 3 Inj.")]
    public void Ohne_n_x_bleibt_der_Text_unveraendert(string text)
    {
        var e = Abbilden(Item($"{text}\n6,71", text, 6.71));

        e.Positionen.ShouldBe([new ExtrahiertePosition(1, text, null, null, null, 6.71m, null)]);
    }

    [Fact]
    public void Eine_von_DI_erkannte_Menge_hat_Vorrang()
    {
        var item = Item("2 x Toner\n90,00", "2 x Toner", 90.00);
        item["valueObject"]!["Quantity"] = new JsonObject { ["type"] = "number", ["valueNumber"] = 1, ["content"] = "1" };

        Abbilden(item).Positionen.ShouldBe([new ExtrahiertePosition(1, "2 x Toner", 1m, null, null, 90.00m, null)]);
    }

    [Fact]
    public void Ohne_Description_kommt_die_Bezeichnung_aus_dem_Positionskopf_vor_dem_Seitenumbruch()
    {
        // Nachgebildet der CLAAS-Mietrechnung 6671476793: Kopf auf Seite 1, Betragszeile auf Seite 2.
        const string volltext = "Pos.\nArtikelnummer\nBezeichnung\nU\nMenge ME\nEinzelpreis\nPE\nWert\n%\n"
            + "aus Auftrag 2210665236 vom 25.11.2026\n0010\n0011977388\nAXION 870 CMATIC - Stage V CEBIS Claas Traktor\n"
            + "Fahrgestellnummer\nA5107495\nBetriebsstunden\n327\nBaujahr\n01/2026\nGeschäftsführer: Bernhard Beer\n"
            + "Hubraum\n6728 cm3\nAnzahl Zylinder\n6\n1 ZUGPENDEL MIT ZUGKUGEL\n";
        const string inhalt = "7 ST\n37,00\n1 ST\n259,00\n19";
        var item = Item(inhalt, "", 259.00);
        var felder = item["valueObject"]!.AsObject();
        felder.Remove("Description");
        felder["Quantity"] = new JsonObject { ["type"] = "number", ["valueNumber"] = 7, ["content"] = "7" };
        felder["Unit"] = new JsonObject { ["type"] = "string", ["valueString"] = "ST", ["content"] = "ST" };
        felder["UnitPrice"] = new JsonObject
        {
            ["type"] = "currency",
            ["valueCurrency"] = new JsonObject { ["amount"] = 37.0, ["currencyCode"] = "EUR" },
            ["content"] = "37,00",
        };
        item["spans"] = new JsonArray(new JsonObject { ["offset"] = volltext.Length, ["length"] = inhalt.Length });

        var e = AbbildenMitVolltext(volltext + inhalt, item);

        e.Positionen.ShouldBe(
            [new ExtrahiertePosition(1, "AXION 870 CMATIC - Stage V CEBIS Claas Traktor", 7m, "ST", 37.00m, 259.00m, null)]);
    }

    [Fact]
    public void Ohne_Description_und_ohne_Positionskopf_bleibt_der_Inhalt_der_Text()
    {
        var item = Item("Pauschale\n12,00", "", 12.00);
        item["valueObject"]!.AsObject().Remove("Description");
        item["spans"] = new JsonArray(new JsonObject { ["offset"] = 9, ["length"] = 15 });

        AbbildenMitVolltext("Rechnung\nPauschale\n12,00", item).Positionen.ShouldHaveSingleItem().Text.ShouldBe("Pauschale\n12,00");
    }

    private static JsonObject Item(string inhalt, string beschreibung, double betrag) => new()
    {
        ["type"] = "object",
        ["content"] = inhalt,
        ["valueObject"] = new JsonObject
        {
            ["Description"] = new JsonObject { ["type"] = "string", ["valueString"] = beschreibung, ["content"] = beschreibung },
            ["Amount"] = new JsonObject
            {
                ["type"] = "currency",
                ["valueCurrency"] = new JsonObject { ["amount"] = betrag, ["currencyCode"] = "EUR" },
                ["content"] = betrag.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-DE")),
            },
        },
    };

    private static ExtraktionsErgebnis Abbilden(params JsonObject[] items) => AbbildenMitVolltext(null, items);

    private static ExtraktionsErgebnis AbbildenMitVolltext(string? volltext, params JsonObject[] items)
    {
        var json = JsonNode.Parse(AzureNachbildung.Antwort)!["analyzeResult"]!;
        if (volltext is not null)
        {
            json["content"] = volltext;
        }

        json["documents"]![0]!["fields"]!["Items"] = new JsonObject
        {
            ["type"] = "array",
            ["valueArray"] = new JsonArray([.. items]),
        };
        return DiClient.Abbilden(ModelReaderWriter.Read<AnalyzeResult>(BinaryData.FromString(json.ToJsonString()))!);
    }
}
