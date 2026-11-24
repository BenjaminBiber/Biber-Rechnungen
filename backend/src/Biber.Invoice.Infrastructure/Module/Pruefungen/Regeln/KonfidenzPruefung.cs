using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>8. Die Erkennungssicherheit von Document Intelligence erreicht die Schwelle aus den Einstellungen.</summary>
public sealed class KonfidenzPruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.Konfidenz;

    public string Name => "Erkennungssicherheit";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        if (e.Beleg.ExtraktionsQuelle != ExtraktionsQuelle.DocumentIntelligence)
        {
            return PruefBefundWert.NichtAnwendbar("Nicht per Texterkennung ausgelesen");
        }

        if (e.Beleg.DiKonfidenz is not { } konfidenz)
        {
            return PruefBefundWert.NichtAnwendbar("Keine Konfidenz geliefert");
        }

        var schwelle = e.Einstellungen.KonfidenzSchwelle;
        return konfidenz < schwelle
            ? PruefBefundWert.Fehler($"Konfidenz {PruefFormat.Zahl(konfidenz)} liegt unter der Schwelle {PruefFormat.Zahl(schwelle)}")
            : PruefBefundWert.Ok($"Konfidenz {PruefFormat.Zahl(konfidenz)} erreicht die Schwelle {PruefFormat.Zahl(schwelle)}");
    }
}
