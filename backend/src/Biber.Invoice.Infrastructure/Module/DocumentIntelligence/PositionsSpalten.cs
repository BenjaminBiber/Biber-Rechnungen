using System.Text.RegularExpressions;
using AzureDi = Azure.AI.DocumentIntelligence;

namespace Biber.Invoice.Infrastructure.Module.DocumentIntelligence;

/// <summary>
/// Artikelnummer und Variante einer Position aus den Tabellen der DI-Antwort. <c>prebuilt-invoice</c> kennt je Item
/// nur <c>ProductCode</c>; eine Größe oder Farbe steht nur in der Tabelle. Gesucht werden Spalten, deren Kopf wie
/// „Artikel-Nr.“ bzw. „Größe“, „Variante“ oder „Farbe“ lautet; zu einem Item gehören die Zellen, deren Text innerhalb
/// seiner Spans liegt (beides sind Offsets in <see cref="AzureDi.AnalyzeResult.Content"/>). Die Spalte „Pos.“ dient nur
/// dazu, einen <c>ProductCode</c> zu erkennen, der in Wahrheit die Positionsnummer ist.
/// </summary>
internal sealed partial class PositionsSpalten
{
    private readonly List<Zelle> _zellen;

    private PositionsSpalten(List<Zelle> zellen) => _zellen = zellen;

    public static PositionsSpalten Aus(IReadOnlyList<AzureDi.DocumentTable>? tabellen)
    {
        var zellen = new List<Zelle>();
        foreach (var (tabelle, t) in (tabellen ?? []).Select((tabelle, t) => (tabelle, t)))
        {
            var kopfzeilen = Kopfzeilen(tabelle);
            var rollen = Rollen(tabelle, kopfzeilen);
            if (rollen.Count == 0)
            {
                continue;
            }

            zellen.AddRange(tabelle.Cells
                .Where(c => !kopfzeilen.Contains(c.RowIndex) && c.Spans?.Count > 0 && rollen.ContainsKey(c.ColumnIndex))
                .Select(c => new Zelle(t, c.RowIndex, c.ColumnIndex, rollen[c.ColumnIndex], c.Spans[0].Offset, (c.Content ?? "").Trim()))
                .Where(z => z.Inhalt.Length > 0));
        }

        return new PositionsSpalten(zellen);
    }

    /// <summary>
    /// Artikelnummer, Variante und Positionsnummer der ersten Tabellenzeile im Bereich des Items; Varianten mehrerer
    /// Spalten mit „/“ verbunden.
    /// </summary>
    public Werte Fuer(AzureDi.DocumentField item)
    {
        if (_zellen.Count == 0 || item.Spans is not { Count: > 0 } spans)
        {
            return new Werte(null, null, null);
        }

        var treffer = _zellen
            .Where(z => spans.Any(s => z.Offset >= s.Offset && z.Offset < s.Offset + s.Length))
            .OrderBy(z => z.Tabelle).ThenBy(z => z.Zeile).ThenBy(z => z.Spalte)
            .ToList();
        var nummer = treffer.FirstOrDefault(z => z.Rolle == Rolle.Artikelnummer)?.Inhalt;
        var positionsnummer = treffer.FirstOrDefault(z => z.Rolle == Rolle.Positionsnummer)?.Inhalt;
        var erste = treffer.FirstOrDefault(z => z.Rolle == Rolle.Variante);
        var variante = erste is null
            ? null
            : string.Join(" / ", treffer
                .Where(z => z.Rolle == Rolle.Variante && z.Tabelle == erste.Tabelle && z.Zeile == erste.Zeile)
                .Select(z => z.Inhalt.ReplaceLineEndings(" ")));
        return new Werte(nummer, variante, positionsnummer);
    }

    public sealed record Werte(string? Artikelnummer, string? Variante, string? Positionsnummer);

    /// <summary>Zeilen mit Spaltenköpfen; ohne ausgezeichnete Köpfe die erste Zeile, wenn sie keine Ziffer enthält.</summary>
    private static HashSet<int> Kopfzeilen(AzureDi.DocumentTable tabelle)
    {
        var kopf = tabelle.Cells.Where(c => c.Kind == AzureDi.DocumentTableCellKind.ColumnHeader).Select(c => c.RowIndex).ToHashSet();
        if (kopf.Count > 0)
        {
            return kopf;
        }

        var erste = tabelle.Cells.Where(c => c.RowIndex == 0).ToList();
        return erste.Count > 0 && !erste.Any(c => (c.Content ?? "").Any(char.IsDigit)) ? [0] : [];
    }

    private static Dictionary<int, Rolle> Rollen(AzureDi.DocumentTable tabelle, HashSet<int> kopfzeilen)
    {
        var koepfe = new Dictionary<int, List<string>>();
        foreach (var zelle in tabelle.Cells.Where(c => kopfzeilen.Contains(c.RowIndex)).OrderBy(c => c.RowIndex))
        {
            for (var spalte = zelle.ColumnIndex; spalte < zelle.ColumnIndex + (zelle.ColumnSpan ?? 1); spalte++)
            {
                if (!koepfe.TryGetValue(spalte, out var teile))
                {
                    koepfe[spalte] = teile = [];
                }

                teile.Add(zelle.Content ?? "");
            }
        }

        var rollen = new Dictionary<int, Rolle>();
        foreach (var (spalte, teile) in koepfe)
        {
            var kopf = Kopf(string.Join(' ', teile));
            if (ArtikelnummerKopf().IsMatch(kopf))
            {
                rollen[spalte] = Rolle.Artikelnummer;
            }
            else if (VarianteKopf().IsMatch(kopf))
            {
                rollen[spalte] = Rolle.Variante;
            }
            else if (PositionsnummerKopf().IsMatch(kopf))
            {
                rollen[spalte] = Rolle.Positionsnummer;
            }
        }

        // Mehrere Nummernspalten (z. B. „Art.-Nr.“ und „Bestell-Nr.“): nur die erste zählt.
        foreach (var spalte in rollen.Where(r => r.Value == Rolle.Artikelnummer).Select(r => r.Key).Order().Skip(1).ToList())
        {
            rollen.Remove(spalte);
        }

        return rollen;
    }

    private static string Kopf(string text) => Leerraum().Replace(text, " ").Trim().TrimEnd(':').Trim().ToLowerInvariant();

    private enum Rolle
    {
        Artikelnummer,
        Variante,
        Positionsnummer,
    }

    private sealed record Zelle(int Tabelle, int Zeile, int Spalte, Rolle Rolle, int Offset, string Inhalt);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Leerraum();

    /// <summary>„Artikel-Nr.“, „Art.-Nr.“, „Artikel #“, „Artikelnummer“, „Bestell-Nr.“, „Material“, „Teile-Nr.“, „SKU“ …</summary>
    [GeneratedRegex(@"^(?:art(?:ikel)?[\s.\-]*(?:nr|nummer|no|#)\.?|bestell[\s.\-]*(?:nr|nummer)\.?|material(?:[\s.\-]*(?:nr\.?|nummer))?|produkt[\s.\-]*(?:nr|nummer)\.?|teile[\s.\-]*(?:nr|nummer)\.?|item[\s.\-]*(?:no|number|#)\.?|sku)$")]
    private static partial Regex ArtikelnummerKopf();

    /// <summary>„Pos.“, „Position“, „Pos.-Nr.“, „Lfd. Nr.“, „Nr.“</summary>
    [GeneratedRegex(@"^(?:pos(?:ition)?\.?(?:[\s.\-]*nr\.?)?|lfd\.?[\s.\-]*nr\.?|nr\.?)$")]
    private static partial Regex PositionsnummerKopf();

    /// <summary>„Größe“, „Gr.“, „Variante“, „Ausführung“, „Farbe“, „Größe/Farbe“, „Size“ …</summary>
    [GeneratedRegex(@"^(?:größe|grösse|groesse|gr\.?|variante|ausführung|ausfuehrung|farbe|size|colou?r)(?:\s*/\s*(?:größe|grösse|groesse|gr\.?|farbe|size|colou?r))?$")]
    private static partial Regex VarianteKopf();
}
