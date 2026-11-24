namespace Biber.Invoice.Domain.Pipeline;

/// <summary>
/// Ein Schritt der Verarbeitungspipeline. Der Orchestrator führt alle registrierten Schritte aufsteigend
/// nach <see cref="Reihenfolge"/> aus und wiederholt bei einem neuen Versuch <b>alle</b> Schritte –
/// ein Schritt muss deshalb idempotent sein (ersetzen statt anhängen).
/// </summary>
public interface IPipelineSchritt
{
    /// <summary>Konstante aus <see cref="SchrittReihenfolge"/>.</summary>
    int Reihenfolge { get; }

    /// <summary>Anzeigename, z. B. „Dubletten-Check“.</summary>
    string Name { get; }

    Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct);
}
