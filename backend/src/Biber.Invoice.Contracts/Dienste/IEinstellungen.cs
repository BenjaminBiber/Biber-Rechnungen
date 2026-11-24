namespace Biber.Invoice.Contracts.Dienste;

/// <summary>
/// Fachliche Einstellungen aus der Datenbank, je Gruppe ein Typ aus <c>Dto/Einstellungen</c>
/// (z. B. <see cref="Dto.Einstellungen.PruefEinstellungen"/>). Fehlt ein Wert, gilt der Standard des Typs.
/// </summary>
public interface IEinstellungen
{
    Task<T> LesenAsync<T>(CancellationToken ct) where T : class, new();

    Task SpeichernAsync<T>(T wert, CancellationToken ct) where T : class;
}
