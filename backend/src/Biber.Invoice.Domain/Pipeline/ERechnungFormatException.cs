namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Das XML einer E-Rechnung ist nicht lesbar oder weder CII noch UBL.</summary>
public sealed class ERechnungFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);
