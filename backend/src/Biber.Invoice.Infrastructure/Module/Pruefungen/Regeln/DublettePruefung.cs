using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>6. Der Beleg ist als mögliche Dublette eines anderen Belegs verknüpft.</summary>
public sealed class DublettePruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.Dublette;

    public string Name => "Dublettenverdacht";

    public PruefBefundWert Pruefen(PruefEingabe e) => e.DuplikatVonNummer is { } nummer
        ? PruefBefundWert.Fehler($"Dublettenverdacht zu {nummer}")
        : PruefBefundWert.Ok("Kein Treffer");
}
