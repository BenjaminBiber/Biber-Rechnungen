using Biber.Invoice.Contracts.Dto.Belege;

namespace Biber.Invoice.Infrastructure.Module.Skonto;

/// <summary>Skonto eines Belegs für Detail und Listen.</summary>
internal static class SkontoAnzeige
{
    /// <summary><c>null</c> ohne Prozentsatz; abgelaufen, wenn die Frist vor <paramref name="heute"/> liegt.</summary>
    public static SkontoDto? Erzeugen(decimal? prozent, DateOnly? bis, decimal? betrag, DateOnly heute) =>
        prozent is { } p ? new SkontoDto(p, bis, betrag, bis < heute) : null;
}
