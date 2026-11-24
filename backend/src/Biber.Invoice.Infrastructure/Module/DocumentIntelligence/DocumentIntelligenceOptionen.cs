namespace Biber.Invoice.Infrastructure.Module.DocumentIntelligence;

/// <summary>Zugang zu Azure Document Intelligence aus der Konfiguration (<c>DocumentIntelligence:*</c>, <c>.env</c>).</summary>
public sealed class DocumentIntelligenceOptionen
{
    public const string Abschnitt = "DocumentIntelligence";

    public string? Endpunkt { get; set; }

    public string? Schluessel { get; set; }

    /// <summary>Höchstdauer einer Analyse einschließlich Abfragen des Ergebnisses.</summary>
    public int TimeoutSekunden { get; set; } = 120;
}
