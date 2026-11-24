namespace Biber.Invoice.Domain.Pipeline;

/// <param name="Satz">Steuersatz in Prozent, z. B. 19.</param>
public sealed record ExtrahierteSteuerzeile(decimal Satz, decimal Netto, decimal Steuer);
