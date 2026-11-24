namespace Biber.Invoice.Domain.Pipeline;

/// <summary>„Erneut auslesen“: verarbeitet einen bereits abgelegten Beleg ab der Formaterkennung neu.</summary>
public interface IVerarbeitungsStarter
{
    Task ErneutVerarbeitenAsync(Guid belegId, CancellationToken ct);
}
