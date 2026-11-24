using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Ergebnis eines <see cref="IPipelineSchritt"/>.</summary>
/// <param name="Endstatus">Status des Import-Vorgangs bei <see cref="SchrittAusgang.Beendet"/>.</param>
/// <param name="Text">Ergebnistext (Beendet) oder Grund (SpaeterErneut, Pausiert).</param>
/// <param name="Nach">Gewünschte Wartezeit bei <see cref="SchrittAusgang.SpaeterErneut"/>; ohne Angabe gilt der Backoff.</param>
public sealed record SchrittErgebnis(SchrittAusgang Ausgang, ImportStatus? Endstatus = null, string? Text = null, TimeSpan? Nach = null)
{
    public static SchrittErgebnis Weiter { get; } = new(SchrittAusgang.Weiter);

    /// <summary>Die Pipeline endet regulär mit <paramref name="status"/>.</summary>
    public static SchrittErgebnis Beendet(ImportStatus status, string text) => new(SchrittAusgang.Beendet, status, text);

    public static SchrittErgebnis SpaeterErneut(string grund, TimeSpan? nach = null) =>
        new(SchrittAusgang.SpaeterErneut, Text: grund, Nach: nach);

    public static SchrittErgebnis Pausiert(string grund) => new(SchrittAusgang.Pausiert, Text: grund);
}
