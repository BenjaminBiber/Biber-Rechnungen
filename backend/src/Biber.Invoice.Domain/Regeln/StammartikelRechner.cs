using System.Globalization;

namespace Biber.Invoice.Domain.Regeln;

/// <summary>Umrechnung von Artikelpreisen auf den Grundpreis eines Stammartikels.</summary>
public static class StammartikelRechner
{
    /// <summary>Dezimalkomma ohne Kulturdaten, damit es auch mit invarianter Globalisierung läuft.</summary>
    private static readonly NumberFormatInfo Deutsch = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    /// <summary>
    /// Preis je <paramref name="grundpreisMenge"/> Basiseinheiten: <c>Einzelpreis / Faktor × Menge</c>,
    /// kaufmännisch auf Cent gerundet. <c>null</c>, wenn Preis oder Faktor fehlen.
    /// </summary>
    public static decimal? Grundpreis(decimal? einzelpreis, decimal? umrechnungFaktor, decimal grundpreisMenge)
    {
        if (einzelpreis is null || umrechnungFaktor is not > 0)
        {
            return null;
        }

        return Math.Round(einzelpreis.Value / umrechnungFaktor.Value * grundpreisMenge, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Gekaufte Menge in Basiseinheiten; ohne Menge oder Faktor 0.</summary>
    public static decimal MengeInBasiseinheiten(decimal? menge, decimal? umrechnungFaktor) =>
        menge is null || umrechnungFaktor is not > 0 ? 0m : menge.Value * umrechnungFaktor.Value;

    /// <summary>„100 Blatt“ bzw. nur die Basiseinheit bei Bezugsmenge 1.</summary>
    public static string GrundpreisEinheit(decimal grundpreisMenge, string basiseinheit) =>
        grundpreisMenge == 1m ? basiseinheit : $"{grundpreisMenge.ToString("0.####", Deutsch)} {basiseinheit}";

    /// <summary>Änderung von <paramref name="von"/> auf <paramref name="bis"/> als Anteil, auf vier Stellen gerundet.</summary>
    public static decimal? Aenderung(decimal? von, decimal? bis)
    {
        if (von is not > 0 || bis is null)
        {
            return null;
        }

        return Math.Round((bis.Value - von.Value) / von.Value, 4, MidpointRounding.AwayFromZero);
    }
}
