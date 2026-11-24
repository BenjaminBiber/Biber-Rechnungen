namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Liest Rechnungen ohne strukturiertes XML mit Azure Document Intelligence (<c>prebuilt-invoice</c>) aus.</summary>
public interface IDocumentIntelligenceClient
{
    /// <summary>Endpunkt und Schlüssel sind gesetzt.</summary>
    bool IstKonfiguriert { get; }

    /// <exception cref="DiVoruebergehendNichtVerfuegbarException">Timeout, 408, 429, 5xx oder Netzwerkfehler.</exception>
    Task<ExtraktionsErgebnis> AnalysierenAsync(Stream datei, CancellationToken ct);

    /// <returns><c>true</c>, wenn der Dienst mit Endpunkt und Schlüssel erreichbar ist.</returns>
    Task<bool> VerbindungTestenAsync(CancellationToken ct);
}

/// <summary>Document Intelligence ist gerade nicht erreichbar; ein späterer Versuch kann gelingen.</summary>
public sealed class DiVoruebergehendNichtVerfuegbarException(string message, Exception? innerException = null)
    : Exception(message, innerException);
