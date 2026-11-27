namespace Biber.Invoice.Domain.Enums;

/// <summary>Woher die Bankleitzahlen stammen: automatisch von der Bundesbank geholt oder von einem Admin hochgeladen.</summary>
public enum BankleitzahlenQuelle
{
    Bundesbank,
    Upload,
}
