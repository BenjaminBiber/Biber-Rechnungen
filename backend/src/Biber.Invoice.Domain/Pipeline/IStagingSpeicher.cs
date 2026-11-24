namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Zwischenablage für angenommene Dateien, bis sie abgelegt sind.</summary>
public interface IStagingSpeicher
{
    /// <summary>Schreibt <paramref name="inhalt"/> ins Staging und berechnet dabei den SHA-256.</summary>
    Task<StagingDatei> SpeichernAsync(Stream inhalt, string dateiname, CancellationToken ct);

    Stream Oeffnen(string pfad);

    void Loeschen(string pfad);
}
