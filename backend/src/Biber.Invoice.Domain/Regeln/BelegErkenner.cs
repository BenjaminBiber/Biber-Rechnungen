using System.Text;
using System.Text.RegularExpressions;

namespace Biber.Invoice.Domain.Regeln;

public enum BelegUrteil
{
    Beleg,
    KeinBeleg,
    Unklar,
}

/// <param name="Grund">Für Benutzer lesbar, z. B. „Dateiname deutet auf AGB“; bei <see cref="BelegUrteil.Unklar"/> leer.</param>
public sealed record BelegEinschaetzung(BelegUrteil Urteil, string Grund);

/// <summary>
/// Schätzt aus Dateiname und Text, ob eine Datei ein Beleg (Rechnung, Gutschrift, Quittung) ist oder etwas, das mit ihm
/// verschickt wird: AGB, Widerrufsbelehrung, Datenschutzhinweise, Lieferschein, Auftragsbestätigung, Angebot, Werbung,
/// Produktinformation. „Kein Beleg“ gilt nur, wenn die Datei eindeutig danach aussieht – im Zweifel <see cref="BelegUrteil.Unklar"/>.
/// <list type="number">
/// <item>Dateiname nennt einen Beleg („Rechnung_123.pdf“) → Beleg.</item>
/// <item>Dateiname nennt etwas anderes („AGB.pdf“) → kein Beleg.</item>
/// <item>Text hat eine Rechnungsnummer mit Wert („Rechnungs-Nr.: 2026-17“) → Beleg.</item>
/// <item>Text beginnt mit etwas anderem („Allgemeine Geschäftsbedingungen“), bevor „Rechnung“ o. Ä. vorkommt → kein Beleg.
/// Weiter hinten zählt so ein Begriff nicht: Dort steht er oft in der Fußzeile einer Rechnung.</item>
/// <item>Text nennt „Rechnung“, „Gutschrift“, „Quittung“ o. Ä. oder Steuer und Eurobetrag → Beleg.</item>
/// </list>
/// </summary>
public static partial class BelegErkenner
{
    /// <summary>Nur so weit vorn im Text zählt ein Begriff wie „Lieferschein“ als Überschrift.</summary>
    public const int Titelbereich = 400;

    /// <summary>Was keine Rechnung ist; Umlaute sind im geprüften Text zum Grundvokal vereinfacht (ä → a).</summary>
    private static readonly (string Art, Regex Muster)[] Andere =
    [
        ("AGB", Wort(@"agbs?|(?:allgemeine\w*\s+)?geschae?ftsbedingungen|nutzungsbedingungen|terms (?:and|&) conditions|terms of (?:service|use)")),
        ("Widerrufsbelehrung", Wort(@"widerruf\w*|cancellation policy")),
        ("Datenschutzhinweise", Wort(@"datenschutz\w*|dsgvo|privacy (?:policy|notice)")),
        ("Lieferschein", Wort(@"lieferschein\w*|packzettel|delivery note|packing (?:slip|list)")),
        ("Auftragsbestätigung", Wort(@"(?:auftrags|bestell)bestae?tigung\w*|bestellue?bersicht|order confirmation")),
        ("Angebot", Wort(@"angebot\w*|kostenvoranschlag\w*|quotation")),
        ("Werbung", Wort(@"newsletter|prospekt\w*|katalog\w*|broschue?re\w*|flyer|werbung")),
        ("Produktinformation", Wort(@"(?:sicherheits)?datenblatt\w*|datasheet|(?:bedienungs|betriebs|montage|gebrauchs)anleitung\w*|gebrauchsanweisung\w*|produktinformation\w*|manual")),
        ("Preisliste", Wort(@"preisliste\w*|price list")),
    ];

    public static BelegEinschaetzung Einschaetzen(string? dateiname, string? text)
    {
        var name = Vereinfachen(Path.GetFileNameWithoutExtension(dateiname ?? ""), nurBuchstaben: true);
        if (BelegWort().IsMatch(name) || BelegImDateinamen().IsMatch(name))
        {
            return new(BelegUrteil.Beleg, "Dateiname nennt einen Beleg");
        }

        if (Erste(name) is { } imNamen)
        {
            return new(BelegUrteil.KeinBeleg, $"Dateiname deutet auf {imNamen.Art}");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new(BelegUrteil.Unklar, "");
        }

        var inhalt = Vereinfachen(text, nurBuchstaben: false);
        if (Rechnungsnummer().IsMatch(inhalt))
        {
            return new(BelegUrteil.Beleg, "Rechnungsnummer im Text");
        }

        var beleg = BelegWort().Match(inhalt);
        if (Erste(inhalt) is { Stelle: < Titelbereich } anderes && (!beleg.Success || anderes.Stelle < beleg.Index))
        {
            return new(BelegUrteil.KeinBeleg, $"Inhalt sieht nach {anderes.Art} aus");
        }

        if (beleg.Success)
        {
            return new(BelegUrteil.Beleg, "Text nennt einen Beleg");
        }

        return Steuer().IsMatch(inhalt) && Eurobetrag().IsMatch(inhalt)
            ? new(BelegUrteil.Beleg, "Steuer und Betrag im Text")
            : new(BelegUrteil.Unklar, "");
    }

    /// <summary>Früheste Stelle, an der der Text etwas anderes als einen Beleg nennt.</summary>
    private static Fund? Erste(string text)
    {
        Fund? erste = null;
        foreach (var (art, muster) in Andere)
        {
            if (muster.Match(text) is { Success: true } treffer && (erste is null || treffer.Index < erste.Stelle))
            {
                erste = new Fund(art, treffer.Index);
            }
        }

        return erste;
    }

    private sealed record Fund(string Art, int Stelle);

    /// <summary>Kleinbuchstaben, Umlaute zum Grundvokal, Leerraum zusammengefasst; für Dateinamen nur Buchstaben.</summary>
    private static string Vereinfachen(string text, bool nurBuchstaben)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var zeichen in text.ToLowerInvariant())
        {
            var c = zeichen switch
            {
                'ä' => 'a',
                'ö' => 'o',
                'ü' => 'u',
                _ => zeichen,
            };
            if (c == 'ß')
            {
                sb.Append("ss");
            }
            else if (char.IsWhiteSpace(c) || (nurBuchstaben && !char.IsLetter(c)))
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Trim();
    }

    private static Regex Wort(string alternativen) =>
        new($@"\b(?:{alternativen})\b", RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>Belegarten als eigenes Wort; „Berechnung“ oder „Aufrechnung“ zählen nicht.</summary>
    [GeneratedRegex(
        @"\b(?:(?:abschlags|teil|schluss|storno|korrektur|sammel|monats|jahres|gesamt|zwischen|end)?rechnung(?:en)?|\w*abrechnung(?:en)?|gutschrift(?:en)?|quittung(?:en)?|kassenbon|kassenbeleg|zahlungsbeleg|invoices?|receipts?|credit notes?)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex BelegWort();

    /// <summary>Im Dateinamen auch zusammengeschrieben („Meinerechnung“), aber nicht „Berechnung“.</summary>
    [GeneratedRegex(@"(?<!be|auf|ver|an|um|zu)rechnung|invoice|gutschrift|quittung|receipt|kassenbon|beleg", RegexOptions.CultureInvariant)]
    private static partial Regex BelegImDateinamen();

    /// <summary>Bezeichnung einer Rechnungsnummer, gefolgt von einer Ziffer („Rechnungs-Nr.: RE-2026-17“), nicht von „§ 5“.</summary>
    [GeneratedRegex(
        @"\b(?:rechnungs?|re\.?|invoice|gutschrift(?:s)?|beleg|quittungs?)[\s.\-]*(?:nummer|number|nr|no)\b[^\d§]{0,25}\d",
        RegexOptions.CultureInvariant)]
    private static partial Regex Rechnungsnummer();

    [GeneratedRegex(@"\b(?:mwst|ust|umsatzsteuer|mehrwertsteuer|vat)\b", RegexOptions.CultureInvariant)]
    private static partial Regex Steuer();

    [GeneratedRegex(@"\d,\d{2}\s?(?:€|eur\b)|(?:€|\beur)\s?\d+[.,]\d{2}", RegexOptions.CultureInvariant)]
    private static partial Regex Eurobetrag();
}
