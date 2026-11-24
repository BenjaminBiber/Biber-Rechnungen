using System.Text.RegularExpressions;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Zuordnung;

/// <summary>Ermittelt die Stelle des ersten Treffers einer Regel im Text – für den Auszug im Regel-Test.</summary>
internal static class TrefferStelle
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    public static (int Start, int Laenge)? Finden(Matching matching, string text)
    {
        if (string.IsNullOrWhiteSpace(matching.Muster))
        {
            return null;
        }

        var optionen = matching.GrossKlein ? RegexOptions.None : RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var muster = matching.Muster;
        return matching.Algorithmus switch
        {
            MatchingAlgorithmus.EinWort or MatchingAlgorithmus.AlleWoerter => muster
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => Suchen(text, $@"(?<!\w){Regex.Escape(w)}(?!\w)", optionen))
                .Where(s => s is not null)
                .OrderBy(s => s!.Value.Start)
                .FirstOrDefault(),
            MatchingAlgorithmus.Exakt or MatchingAlgorithmus.Unscharf => text.IndexOf(
                muster, matching.GrossKlein ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) is var i and >= 0
                ? (i, muster.Length)
                : null,
            MatchingAlgorithmus.Regex => Suchen(text, muster, optionen),
            _ => null,
        };
    }

    private static (int Start, int Laenge)? Suchen(string text, string ausdruck, RegexOptions optionen)
    {
        try
        {
            var treffer = Regex.Match(text, ausdruck, optionen, RegexTimeout);
            return treffer.Success ? (treffer.Index, treffer.Length) : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }
}
