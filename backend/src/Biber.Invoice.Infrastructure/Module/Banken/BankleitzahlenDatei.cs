using System.Text;
using System.Text.RegularExpressions;
using Biber.Invoice.Domain.Entitaeten;

namespace Biber.Invoice.Infrastructure.Module.Banken;

/// <summary>
/// Liest die Bankleitzahlendatei der Deutschen Bundesbank im TXT-Format: feste Satzlänge, ISO-8859-1, Stelle 1–8
/// Bankleitzahl, 9 Merkmal (1 = Hauptstelle, 2 = Filiale), 10–67 Bezeichnung. Übernommen werden nur Hauptstellen.
/// </summary>
public static partial class BankleitzahlenDatei
{
    /// <summary>
    /// Eine echte Datei hat rund 3 500 Hauptstellen; weniger heißt, es ist die falsche Datei – dann bleibt die Tabelle,
    /// wie sie ist, statt geleert zu werden.
    /// </summary>
    public const int MindestAnzahl = 1000;

    private const string KeineBlzDatei =
        "Das ist keine Bankleitzahlendatei der Bundesbank im TXT-Format (z. B. „blz-aktuell-txt-data.txt“).";

    public static IReadOnlyList<Bank> Lesen(byte[] inhalt)
    {
        var banken = new Dictionary<string, Bank>(StringComparer.Ordinal);
        foreach (var zeile in Dekodieren(inhalt).Split('\n'))
        {
            var satz = zeile.TrimEnd('\r');
            if (satz.Length == 0)
            {
                continue;
            }

            if (satz.Length < 67 || !Bankleitzahl().IsMatch(satz[..8]) || satz[8] is not ('1' or '2'))
            {
                throw new BankleitzahlenAusnahme(KeineBlzDatei);
            }

            if (satz[8] == '1')
            {
                var blz = satz[..8];
                banken[blz] = new Bank { Blz = blz, Name = AltZusatz().Replace(satz[9..67].TrimEnd(), "") };
            }
        }

        if (banken.Count < MindestAnzahl)
        {
            throw new BankleitzahlenAusnahme(banken.Count == 0
                ? KeineBlzDatei
                : $"Die Datei enthält nur {banken.Count} Banken – erwartet ist die vollständige Bankleitzahlendatei.");
        }

        return [.. banken.Values];
    }

    /// <summary>Die Bundesbank liefert ISO-8859-1; eine in einem Editor als UTF-8 gespeicherte Datei geht auch.</summary>
    private static string Dekodieren(byte[] inhalt)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(inhalt).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(inhalt);
        }
    }

    [GeneratedRegex("^[0-9]{8}$")]
    private static partial Regex Bankleitzahl();

    /// <summary>„-alt-“ kennzeichnet ein Institut, das nach einer Fusion aufgegangen ist; die BLZ bleibt gültig.</summary>
    [GeneratedRegex(@" ?-alt-\.?$")]
    private static partial Regex AltZusatz();
}

/// <summary>Die Bankleitzahlen ließen sich nicht laden: falsche Datei oder Bundesbank nicht erreichbar.</summary>
public sealed class BankleitzahlenAusnahme(string meldung, Exception? ursache = null) : Exception(meldung, ursache);
