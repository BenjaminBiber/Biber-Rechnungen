using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using Azure.AI.DocumentIntelligence;
using Biber.Invoice.Domain.Entitaeten;
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
    public void Strich_Items_ohne_Betrag_fallen_weg()
    {
        // VetStar-Rechnung 20433/2086: Vor jeder Datumszeile steht ein 1-pt-Strich, den DI als eigenes Item liefert.
        var e = Abbilden(
            ItemOhneBetrag("-", "-"),
            Item("03.09.2026\nRind Euthanasie\n35,94", "Rind Euthanasie", 35.94),
            ItemOhneBetrag("-\n21.09.2026", null),
            Item("30 x Trächtigkeitsuntersuchung\n231,00", "30 x Trächtigkeitsuntersuchung", 231.00));

        e.Positionen.ShouldBe(
        [
            new ExtrahiertePosition(1, "Rind Euthanasie", null, null, null, 35.94m, null),
            new ExtrahiertePosition(2, "Trächtigkeitsuntersuchung", 30m, null, 7.70m, 231.00m, null),
        ]);
    }

    [Fact]
    public void Strich_und_Datum_vor_der_Bezeichnung_fallen_aus_dem_Text()
    {
        var e = Abbilden(Item("-\n24.09.2026\nRind Euthanasie\n35,94", "-\n24.09.2026\nRind Euthanasie", 35.94));

        e.Positionen.ShouldBe([new ExtrahiertePosition(1, "Rind Euthanasie", null, null, null, 35.94m, null)]);
    }

    [Fact]
    public void Ein_Strich_mit_Betrag_bleibt_als_Position_erhalten()
    {
        var e = Abbilden(Item("-\n12,00", "-", 12.00));

        e.Positionen.ShouldBe([new ExtrahiertePosition(1, "-", null, null, null, 12.00m, null)]);
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
            [new ExtrahiertePosition(1, "AXION 870 CMATIC - Stage V CEBIS Claas Traktor", 7m, "ST", 37.00m, 259.00m, null, "0011977388")]);
    }

    [Fact]
    public void Ohne_Description_und_ohne_Positionskopf_bleibt_der_Inhalt_der_Text()
    {
        var item = Item("Pauschale\n12,00", "", 12.00);
        item["valueObject"]!.AsObject().Remove("Description");
        item["spans"] = new JsonArray(new JsonObject { ["offset"] = 9, ["length"] = 15 });

        AbbildenMitVolltext("Rechnung\nPauschale\n12,00", item).Positionen.ShouldHaveSingleItem().Text.ShouldBe("Pauschale\n12,00");
    }

    [Fact]
    public void ProductCode_wird_die_Artikelnummer_und_die_Spalte_Groesse_ein_Merkmal()
    {
        // Nachgebildet der Strauss-Rechnung R187502621: dieselbe Artikelnummer in zwei Größen, DI zieht die Größe
        // teils mit in die Beschreibung.
        var t = new Tabelle(
            ["Artikel-Nr.", "Größe", "Artikel", "MwSt. %", "Menge", "Einzelpreis\nnetto", "Gesamtpreis\nnetto"],
            ["93435", "47", "S3 Sicherheitshalbschuhe Willingen\ngrau/marine/schwarz", "19,00", "1", "56,90", "56,90"],
            ["93435", "42", "S3 Sicherheitshalbschuhe Willingen\ngrau/marine/schwarz", "19,00", "1", "56,90", "56,90"]);
        var erste = t.Item(0, "S3 Sicherheitshalbschuhe Willingen\ngrau/marine/schwarz", 56.90, produktcode: "93435");
        var zweite = t.Item(1, "42 S3 Sicherheitshalbschuhe Willingen\ngrau/marine/schwarz", 56.90, produktcode: "93435");

        var e = AbbildenMitTabelle(t.Volltext, t.Json, erste, zweite);

        e.Positionen.Select(p => (p.Artikelnummer, MerkmalText(p))).ShouldBe([("93435", "Größe: 47"), ("93435", "Größe: 42")]);
        e.Positionen.Select(p => p.Text).ShouldAllBe(text => text == "S3 Sicherheitshalbschuhe Willingen\ngrau/marine/schwarz");
    }

    [Fact]
    public void Ohne_ProductCode_kommt_die_Artikelnummer_aus_der_Tabellenspalte()
    {
        // Nachgebildet der Hefele-Rechnung R26-1285: gleiche Bezeichnung, verschiedene Artikelnummern. „Art“ (E/W/S)
        // ist weder Nummer noch Merkmal.
        var t = new Tabelle(
            ["Pos.", "Artikel #", "Art", "Bezeichnung", "Menge", "EP/Netto", "GesamtEUR"],
            ["26", "5G12FDLORX45", "E", "Pressnippel Megacr.", "1,00", "23,27", "23,27"],
            ["27", "5G8FDLORX", "E", "Pressnippel Megacr.", "1,00", "10,70", "10,70"]);

        // DI hält manchmal die Positionsnummer für den ProductCode; die Spalte „Artikel #“ hat Vorrang.
        var e = AbbildenMitTabelle(
            t.Volltext, t.Json, t.Item(0, "Pressnippel Megacr.", 23.27, produktcode: "26"), t.Item(1, "Pressnippel Megacr.", 10.70));

        e.Positionen.Select(p => (p.Text, p.Artikelnummer, MerkmalText(p))).ShouldBe(
            [("Pressnippel Megacr.", "5G12FDLORX45", null), ("Pressnippel Megacr.", "5G8FDLORX", null)]);
    }

    [Fact]
    public void Steht_die_Groesse_im_ProductCode_wird_sie_abgetrennt()
    {
        var t = new Tabelle(["Art.-Nr.", "Gr.", "Bezeichnung", "Betrag"], ["89583", "3XL", "Microfleece Troyer", "19,70"]);

        var e = AbbildenMitTabelle(t.Volltext, t.Json, t.Item(0, "89583 3XL Microfleece Troyer", 19.70, produktcode: "89583 3XL"));

        var p = e.Positionen.ShouldHaveSingleItem();
        (p with { Merkmale = null }).ShouldBe(new ExtrahiertePosition(1, "Microfleece Troyer", null, null, null, 19.70m, null, "89583"));
        MerkmalText(p).ShouldBe("Größe: 3XL");
    }

    [Fact]
    public void Ein_ProductCode_gleich_der_Positionsnummer_ist_keine_Artikelnummer()
    {
        var t = new Tabelle(["Pos.", "Bezeichnung", "Menge", "Betrag"], ["4", "Frachtkosten", "1,00", "6,50"]);

        var p = AbbildenMitTabelle(t.Volltext, t.Json, t.Item(0, "Frachtkosten", 6.50, produktcode: "4")).Positionen.ShouldHaveSingleItem();

        p.Artikelnummer.ShouldBeNull();
        p.Text.ShouldBe("Frachtkosten");
    }

    [Fact]
    public void Ohne_Tabelle_bleibt_der_ProductCode_die_Artikelnummer()
    {
        var item = Item("SL1005\nKraftstoffschlauch 5mm GP5\n1,06", "Kraftstoffschlauch 5mm GP5", 1.06);
        item["valueObject"]!["ProductCode"] = new JsonObject { ["type"] = "string", ["valueString"] = "SL1005", ["content"] = "SL1005" };

        Abbilden(item).Positionen.ShouldHaveSingleItem().Artikelnummer.ShouldBe("SL1005");
    }

    [Fact]
    public void Jede_Merkmalspalte_ergibt_ein_Merkmal_mit_dem_Spaltenkopf_als_Namen()
    {
        var t = new Tabelle(["Artikelnummer", "Farbe", "Größe", "Bezeichnung", "Betrag"], ["A-100", "schwarz", "XL", "Fleecejacke", "39,90"]);

        var p = AbbildenMitTabelle(t.Volltext, t.Json, t.Item(0, "Fleecejacke", 39.90)).Positionen.ShouldHaveSingleItem();

        p.Artikelnummer.ShouldBe("A-100");
        p.Merkmale.ShouldNotBeNull().ShouldBe([new Merkmal("Farbe", "schwarz"), new Merkmal("Größe", "XL")]);
    }

    [Fact]
    public void Ohne_passende_Spaltenkoepfe_bleiben_Artikelnummer_und_Merkmale_leer()
    {
        var t = new Tabelle(["Beschreibung", "Menge", "Betrag"], ["Wartung", "1", "50,00"]);

        var p = AbbildenMitTabelle(t.Volltext, t.Json, t.Item(0, "Wartung", 50.00)).Positionen.ShouldHaveSingleItem();

        p.Artikelnummer.ShouldBeNull();
        p.Merkmale.ShouldBeNull();
    }

    /// <summary>„Größe: 47, Farbe: grau“; <c>null</c> ohne Merkmale.</summary>
    private static string? MerkmalText(ExtrahiertePosition p) => p.Merkmale is null ? null : string.Join(", ", p.Merkmale);

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

    private static JsonObject ItemOhneBetrag(string inhalt, string? beschreibung)
    {
        var felder = new JsonObject();
        if (beschreibung is not null)
        {
            felder["Description"] = new JsonObject { ["type"] = "string", ["valueString"] = beschreibung, ["content"] = beschreibung };
        }

        return new JsonObject { ["type"] = "object", ["content"] = inhalt, ["valueObject"] = felder };
    }

    private static ExtraktionsErgebnis Abbilden(params JsonObject[] items) => AbbildenMitVolltext(null, items);

    private static ExtraktionsErgebnis AbbildenMitVolltext(string? volltext, params JsonObject[] items) =>
        AbbildenMitTabelle(volltext, null, items);

    private static ExtraktionsErgebnis AbbildenMitTabelle(string? volltext, JsonObject? tabelle, params JsonObject[] items)
    {
        var json = JsonNode.Parse(AzureNachbildung.Antwort)!["analyzeResult"]!;
        if (volltext is not null)
        {
            json["content"] = volltext;
        }

        if (tabelle is not null)
        {
            json["tables"] = new JsonArray(tabelle);
        }

        json["documents"]![0]!["fields"]!["Items"] = new JsonObject
        {
            ["type"] = "array",
            ["valueArray"] = new JsonArray([.. items]),
        };
        return DiClient.Abbilden(ModelReaderWriter.Read<AnalyzeResult>(BinaryData.FromString(json.ToJsonString()))!);
    }

    /// <summary>
    /// DI-Tabelle mit Kopfzeile wie in <c>analyzeResult.tables</c>; jede Zelle steht als eigene Zeile im Volltext, die
    /// Spans der Zellen und Items zeigen darauf.
    /// </summary>
    private sealed class Tabelle
    {
        private readonly List<(int Beginn, int Ende)> _zeilen = [];

        public Tabelle(string[] kopf, params string[][] zeilen)
        {
            var text = new System.Text.StringBuilder();
            var zellen = new JsonArray();
            foreach (var (werte, zeile) in new[] { kopf }.Concat(zeilen).Select((w, i) => (w, i)))
            {
                var beginn = text.Length;
                foreach (var (wert, spalte) in werte.Select((w, i) => (w, i)))
                {
                    zellen.Add(new JsonObject
                    {
                        ["kind"] = zeile == 0 ? "columnHeader" : "content",
                        ["rowIndex"] = zeile,
                        ["columnIndex"] = spalte,
                        ["content"] = wert,
                        ["spans"] = new JsonArray(new JsonObject { ["offset"] = text.Length, ["length"] = wert.Length }),
                    });
                    text.Append(wert).Append('\n');
                }

                if (zeile > 0)
                {
                    _zeilen.Add((beginn, text.Length - 1));
                }
            }

            Volltext = text.ToString();
            Json = new JsonObject
            {
                ["rowCount"] = zeilen.Length + 1,
                ["columnCount"] = kopf.Length,
                ["cells"] = zellen,
                ["spans"] = new JsonArray(new JsonObject { ["offset"] = 0, ["length"] = Volltext.Length }),
            };
        }

        public string Volltext { get; }

        public JsonObject Json { get; }

        /// <summary>Item über die Datenzeile <paramref name="zeile"/> (ab 0), optional mit <c>ProductCode</c>.</summary>
        public JsonObject Item(int zeile, string beschreibung, double betrag, string? produktcode = null)
        {
            var (beginn, ende) = _zeilen[zeile];
            var item = DiPositionenTests.Item(Volltext[beginn..ende], beschreibung, betrag);
            item["spans"] = new JsonArray(new JsonObject { ["offset"] = beginn, ["length"] = ende - beginn });
            if (produktcode is not null)
            {
                item["valueObject"]!["ProductCode"] = new JsonObject
                {
                    ["type"] = "string",
                    ["valueString"] = produktcode,
                    ["content"] = produktcode,
                };
            }

            return item;
        }
    }
}
