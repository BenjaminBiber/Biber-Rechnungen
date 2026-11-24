namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Zugriff auf den KoSIT-Sidecar: Validierung und HTML-Visualisierung von E-Rechnungen.</summary>
public interface IKositClient
{
    /// <exception cref="KositNichtErreichbarException">Verbindungsfehler, Timeout oder 5xx.</exception>
    Task<KositErgebnis> ValidierenAsync(byte[] xml, CancellationToken ct);

    /// <exception cref="KositNichtErreichbarException">Verbindungsfehler, Timeout oder 5xx.</exception>
    Task<string> VisualisierenAsync(byte[] xml, CancellationToken ct);
}

/// <summary>Der KoSIT-Sidecar ist vorübergehend nicht erreichbar; der Schritt wird später wiederholt.</summary>
public sealed class KositNichtErreichbarException(string message, Exception? innerException = null)
    : Exception(message, innerException);
