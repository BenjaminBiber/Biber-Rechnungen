using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.Infrastructure.Module.Korrespondenten;

/// <summary>Vergleicht die Bankverbindungen eines Belegs mit denen des Korrespondenten und ergänzt sie.</summary>
public static class BankverbindungenAbgleich
{
    /// <summary>IBANs aus <paramref name="erkannt"/> (ohne Leerzeichen, groß), die der Korrespondent nicht kennt.</summary>
    public static IReadOnlyList<string> Unbekannte(Korrespondent korrespondent, IEnumerable<Bankverbindung> erkannt)
    {
        var bekannt = korrespondent.Bankverbindungen.Select(b => KorrespondentNormalisierung.Kennung(b.Iban)).OfType<string>().ToHashSet();
        return [.. erkannt.Select(b => KorrespondentNormalisierung.Kennung(b.Iban)).OfType<string>().Distinct().Where(iban => !bekannt.Contains(iban))];
    }

    /// <summary>
    /// Trägt zu bekannten IBANs einen fehlenden BIC nach; unbekannte IBANs werden nur mit
    /// <paramref name="neueUebernehmen"/> angehängt. Vorhandene Werte bleiben.
    /// </summary>
    public static void Ergaenzen(Korrespondent korrespondent, IEnumerable<Bankverbindung> erkannt, bool neueUebernehmen)
    {
        var liste = korrespondent.Bankverbindungen.ToList();
        var geaendert = false;
        foreach (var verbindung in erkannt)
        {
            if (KorrespondentNormalisierung.Kennung(verbindung.Iban) is not { } iban)
            {
                continue;
            }

            var bic = KorrespondentNormalisierung.Kennung(verbindung.Bic) is { } b && KorrespondentNormalisierung.IstBic(b) ? b : null;
            var index = liste.FindIndex(v => KorrespondentNormalisierung.Kennung(v.Iban) == iban);
            if (index < 0)
            {
                if (neueUebernehmen)
                {
                    liste.Add(new Bankverbindung(iban, bic));
                    geaendert = true;
                }
            }
            else if (bic is not null && string.IsNullOrWhiteSpace(liste[index].Bic))
            {
                liste[index] = liste[index] with { Bic = bic };
                geaendert = true;
            }
        }

        if (geaendert)
        {
            korrespondent.Bankverbindungen = liste; // neue Liste, damit EF die Änderung sieht
        }
    }
}
