namespace Biber.Invoice.Infrastructure.Module.Pipeline;

/// <summary>
/// Leitet eine einzelne Steuerzeile aus Netto, USt und Brutto ab, wenn die Extraktion keine Aufschlüsselung nach
/// Steuersätzen liefert. Nur wenn Netto + USt = Brutto ergibt und die USt genau zu einem üblichen Satz passt –
/// sonst bleibt der Beleg ohne Steuerzeile (gemischte Sätze lassen sich so nicht erkennen).
/// </summary>
public static class SteuerzeileAbleitung
{
    /// <summary>Abweichung in Euro, bis zu der Summe und Steuer als „passt“ gelten (wie im Frontend).</summary>
    public const decimal Toleranz = 0.02m;

    /// <summary>Geprüfte Sätze in dieser Reihenfolge; 16 % und 5 % galten im zweiten Halbjahr 2020.</summary>
    public static readonly decimal[] Saetze = [19m, 7m, 16m, 5m, 0m];

    /// <summary>Der passende Steuersatz, oder <c>null</c>, wenn sich keine eindeutige Zeile ableiten lässt.</summary>
    public static decimal? Satz(decimal? netto, decimal? steuer, decimal? brutto)
    {
        if (netto is not { } n || steuer is not { } s || brutto is not { } b || n == 0m)
        {
            return null;
        }

        if (Math.Abs(n + s - b) > Toleranz)
        {
            return null;
        }

        foreach (var satz in Saetze)
        {
            if (Math.Abs(Math.Round(n * satz / 100m, 2, MidpointRounding.AwayFromZero) - s) <= Toleranz)
            {
                return satz;
            }
        }

        return null;
    }
}
