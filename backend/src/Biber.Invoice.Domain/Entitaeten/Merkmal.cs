namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>
/// Merkmal einer Position wie Größe oder Farbe; gehört zur Position, nicht zum Artikel. <see cref="Name"/> fehlt, wenn
/// der Beleg nur den Wert nennt.
/// </summary>
public sealed record Merkmal(string? Name, string Wert)
{
    /// <summary>„Größe: 42“ bzw. nur der Wert – so steht das Merkmal im Änderungsprotokoll.</summary>
    public override string ToString() => Name is null ? Wert : $"{Name}: {Wert}";
}
