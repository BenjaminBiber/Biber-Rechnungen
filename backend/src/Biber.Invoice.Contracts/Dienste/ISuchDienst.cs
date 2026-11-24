using Biber.Invoice.Contracts.Dto.Sonstiges;

namespace Biber.Invoice.Contracts.Dienste;

public interface ISuchDienst
{
    /// <summary>
    /// Globale Suche der Befehlspalette: je Gruppe (Belege, Korrespondenten, Artikel, Stammartikel) höchstens
    /// <see cref="MaxTreffer"/> Treffer. <paramref name="q"/> wird getrimmt; unter <see cref="MinLaenge"/> Zeichen
    /// ist das Ergebnis leer.
    /// </summary>
    Task<SuchergebnisDto> SuchenAsync(string? q, CancellationToken ct);

    public const int MinLaenge = 2;
    public const int MaxLaenge = 100;
    public const int MaxTreffer = 5;
}
