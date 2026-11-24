namespace Biber.Invoice.Infrastructure.Module.Artikelpreise;

/// <summary>Ein Artikel ist bereits einem anderen Stammartikel zugeordnet (409).</summary>
public sealed class StammartikelKonfliktException(string meldung) : Exception(meldung);

/// <summary>Die Anfrage verweist auf Artikel, die es nicht gibt (400 mit <see cref="Fehler"/>).</summary>
public sealed class StammartikelUngueltigException(IReadOnlyDictionary<string, string[]> fehler)
    : Exception("Ungültige Zuordnung.")
{
    public IReadOnlyDictionary<string, string[]> Fehler { get; } = fehler;
}
