namespace Biber.Invoice.Infrastructure.Module.Logos;

/// <summary>Fachlicher Fehler beim Setzen eines Logos; <see cref="Status"/> ist der HTTP-Status der Antwort.</summary>
public sealed class LogoAusnahme(int status, string titel, string? feld = null) : Exception(titel)
{
    public const int Ungueltig = 400;
    public const int NichtGefunden = 404;
    public const int Konflikt = 409;

    public int Status { get; } = status;

    /// <summary>Feld für die Validierungsantwort (nur bei <see cref="Ungueltig"/>).</summary>
    public string? Feld { get; } = feld;
}
