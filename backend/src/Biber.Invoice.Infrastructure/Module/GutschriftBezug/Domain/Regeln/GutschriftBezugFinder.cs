using System.Text.RegularExpressions;
using Biber.Invoice.Infrastructure.Module.FachlicheDublette.Domain.Regeln;

namespace Biber.Invoice.Infrastructure.Module.GutschriftBezug.Domain.Regeln;

/// <summary>Eine Rechnung desselben Korrespondenten, auf die sich eine Gutschrift beziehen könnte.</summary>
public sealed record RechnungKandidat(Guid Id, string Rechnungsnummer);

/// <summary>Findet die Rechnung zu einer Gutschrift über die angegebene Bezugsnummer oder eine Nummer im Volltext.</summary>
public static class GutschriftBezugFinder
{
    /// <summary>Kürzere Nummern (normalisiert) kommen im Volltext zu oft zufällig vor.</summary>
    public const int MinLaengeImText = 4;

    /// <summary>
    /// Die eindeutig passende Rechnung: zuerst über <paramref name="bezugNummer"/> (E-Rechnung, BT-25), sonst über eine
    /// Rechnungsnummer, die als eigenes Wort im <paramref name="volltext"/> steht. Mehrdeutige Treffer ergeben <c>null</c>.
    /// </summary>
    public static Guid? Finden(
        string? bezugNummer, string? volltext, string? eigeneNummer, IReadOnlyList<RechnungKandidat> kandidaten)
    {
        var eigene = RechnungsnummerNormalisierer.Normalisieren(eigeneNummer);
        var nutzbar = kandidaten
            .Select(k => (k.Id, k.Rechnungsnummer, Norm: RechnungsnummerNormalisierer.Normalisieren(k.Rechnungsnummer)))
            .Where(k => k.Norm is not null && k.Norm != eigene)
            .ToList();

        if (RechnungsnummerNormalisierer.Normalisieren(bezugNummer) is { } bezug)
        {
            var treffer = nutzbar.Where(k => k.Norm == bezug).Select(k => k.Id).Distinct().ToList();
            if (treffer.Count == 1)
            {
                return treffer[0];
            }
        }

        if (string.IsNullOrWhiteSpace(volltext))
        {
            return null;
        }

        var imText = nutzbar
            .Where(k => k.Norm!.Length >= MinLaengeImText && ImText(volltext, k.Rechnungsnummer))
            .Select(k => k.Id)
            .Distinct()
            .ToList();
        return imText.Count == 1 ? imText[0] : null;
    }

    private static bool ImText(string volltext, string nummer) =>
        Regex.IsMatch(
            volltext,
            $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(nummer.Trim())}(?![\p{{L}}\p{{N}}])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
}
