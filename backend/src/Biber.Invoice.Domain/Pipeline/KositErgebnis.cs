namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Ergebnis der KoSIT-Validierung einer E-Rechnung.</summary>
public sealed record KositErgebnis(bool Gueltig, IReadOnlyList<string> Meldungen);
