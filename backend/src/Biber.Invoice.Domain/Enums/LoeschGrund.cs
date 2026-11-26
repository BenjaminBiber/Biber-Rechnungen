namespace Biber.Invoice.Domain.Enums;

/// <summary>
/// Warum ein Beleg trotz Löschsperre in die gelöschten Belege verschoben wurde. Einziger zulässiger Grund: Er gehört
/// nicht in dieses Archiv (falsch hochgeladen oder falsch zugeordnet).
/// </summary>
public enum LoeschGrund
{
    FalschZugeordnet,
}
