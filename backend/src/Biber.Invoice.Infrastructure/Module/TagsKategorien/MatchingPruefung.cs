using System.Text.RegularExpressions;
using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.TagsKategorien;

/// <summary>Prüft eine Zuordnungsregel und überträgt sie auf die Entität.</summary>
public static class MatchingPruefung
{
    public static Fehler? Pruefe(MatchingDto? matching)
    {
        if (matching is null)
        {
            return Fehler.Ungueltig("matching", "Die Zuordnungsregel fehlt.");
        }

        if (!Enum.IsDefined(matching.Algorithmus))
        {
            return Fehler.Ungueltig("matching.algorithmus", "Unbekannter Algorithmus.");
        }

        if (matching.Algorithmus is MatchingAlgorithmus.Keiner or MatchingAlgorithmus.Auto)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(matching.Muster))
        {
            return Fehler.Ungueltig("matching.muster", "Das Muster ist Pflicht.");
        }

        if (matching.Algorithmus == MatchingAlgorithmus.Regex)
        {
            try
            {
                _ = new Regex(matching.Muster, RegexOptions.None, TimeSpan.FromMilliseconds(200));
            }
            catch (ArgumentException)
            {
                return Fehler.Ungueltig("matching.muster", "Der reguläre Ausdruck ist ungültig.");
            }
        }

        return null;
    }

    public static Matching ZuEntitaet(MatchingDto dto) => new()
    {
        Algorithmus = dto.Algorithmus,
        Muster = dto.Algorithmus is MatchingAlgorithmus.Keiner or MatchingAlgorithmus.Auto ? null : dto.Muster,
        GrossKlein = dto.GrossKlein,
    };

    public static MatchingDto ZuDto(Matching m) => new(m.Algorithmus, m.Muster, m.GrossKlein);
}
