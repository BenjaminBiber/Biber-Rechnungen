using System.Text.RegularExpressions;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>
/// Zuordnungsregel für Tags, Kategorien und Korrespondenten nach dem Vorbild von paperless-ngx (Owned-Typ).
/// </summary>
public class Matching
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);
    private const double MindestAehnlichkeit = 0.85;

    public MatchingAlgorithmus Algorithmus { get; set; }
    public string? Muster { get; set; }
    public bool GrossKlein { get; set; }

    /// <summary>
    /// Prüft, ob <paramref name="text"/> die Regel erfüllt. <see cref="MatchingAlgorithmus.Keiner"/> und
    /// <see cref="MatchingAlgorithmus.Auto"/> (ML-Modell) treffen hier nie.
    /// </summary>
    public bool Trifft(string? text)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(Muster))
        {
            return false;
        }

        return Algorithmus switch
        {
            MatchingAlgorithmus.EinWort => Woerter().Any(w => EnthaeltWort(text, w)),
            MatchingAlgorithmus.AlleWoerter => Woerter().All(w => EnthaeltWort(text, w)),
            MatchingAlgorithmus.Exakt => text.Contains(Muster, GrossKlein ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase),
            MatchingAlgorithmus.Regex => TrifftRegex(text, Muster),
            MatchingAlgorithmus.Unscharf => TrifftUnscharf(text, Muster),
            _ => false,
        };
    }

    private RegexOptions Optionen => GrossKlein ? RegexOptions.None : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private string[] Woerter() => Muster!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private bool EnthaeltWort(string text, string wort) =>
        TrifftRegex(text, $@"(?<!\w){Regex.Escape(wort)}(?!\w)");

    private bool TrifftRegex(string text, string ausdruck)
    {
        try
        {
            return Regex.IsMatch(text, ausdruck, Optionen, RegexTimeout);
        }
        catch (ArgumentException)
        {
            return false; // ungültiger Ausdruck
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private bool TrifftUnscharf(string text, string muster)
    {
        if (!GrossKlein)
        {
            text = text.ToLowerInvariant();
            muster = muster.ToLowerInvariant();
        }

        if (text.Contains(muster, StringComparison.Ordinal))
        {
            return true;
        }

        // Fenster der Länge des Musters ± erlaubte Abweichung über den Text schieben.
        var erlaubt = (int)Math.Floor(muster.Length * (1 - MindestAehnlichkeit));
        for (var laenge = Math.Max(1, muster.Length - erlaubt); laenge <= muster.Length + erlaubt; laenge++)
        {
            for (var start = 0; start + laenge <= text.Length; start++)
            {
                var fenster = text.AsSpan(start, laenge);
                var aehnlichkeit = 1 - ((double)Levenshtein(fenster, muster) / Math.Max(laenge, muster.Length));
                if (aehnlichkeit >= MindestAehnlichkeit)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int Levenshtein(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        var vorher = new int[b.Length + 1];
        var aktuell = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            vorher[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            aktuell[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var kosten = a[i - 1] == b[j - 1] ? 0 : 1;
                aktuell[j] = Math.Min(Math.Min(aktuell[j - 1] + 1, vorher[j] + 1), vorher[j - 1] + kosten);
            }

            (vorher, aktuell) = (aktuell, vorher);
        }

        return vorher[b.Length];
    }
}
