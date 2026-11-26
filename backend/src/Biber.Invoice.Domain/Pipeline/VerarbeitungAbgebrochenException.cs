namespace Biber.Invoice.Domain.Pipeline;

/// <summary>
/// „Erneut auslesen“ endete vorzeitig, weil ein Schritt nicht <see cref="SchrittAusgang.Weiter"/> lieferte
/// (z. B. <see cref="SchrittAusgang.Pausiert"/> beim Document-Intelligence-Limit).
/// </summary>
public sealed class VerarbeitungAbgebrochenException(string schritt, SchrittAusgang ausgang, string? grund)
    : InvalidOperationException($"Erneutes Auslesen bei Schritt „{schritt}“ abgebrochen: {grund}")
{
    public string Schritt { get; } = schritt;

    public SchrittAusgang Ausgang { get; } = ausgang;

    public string? Grund { get; } = grund;
}
