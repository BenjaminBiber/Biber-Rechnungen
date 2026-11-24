using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>Eine Plausibilitätsprüfung ohne Seiteneffekte; alles, was sie braucht, steht in der <see cref="PruefEingabe"/>.</summary>
public interface IPruefung
{
    PruefungArt Art { get; }

    /// <summary>Anzeigename wie im Tab „Prüfung“, z. B. „Datum plausibel“.</summary>
    string Name { get; }

    PruefBefundWert Pruefen(PruefEingabe e);
}

/// <summary>Ergebnis einer Prüfung mit dem Detailtext für die Oberfläche.</summary>
public sealed record PruefBefundWert(PruefErgebnis Ergebnis, string Detail)
{
    public static PruefBefundWert Ok(string detail) => new(PruefErgebnis.Ok, detail);

    public static PruefBefundWert Fehler(string detail) => new(PruefErgebnis.Fehlgeschlagen, detail);

    public static PruefBefundWert NichtAnwendbar(string detail) => new(PruefErgebnis.NichtAnwendbar, detail);
}
