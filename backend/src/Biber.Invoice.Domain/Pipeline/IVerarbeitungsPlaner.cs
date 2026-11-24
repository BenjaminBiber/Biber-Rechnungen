namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Plant die Verarbeitung eines Import-Vorgangs ein (Hangfire-Queue „verarbeitung“).</summary>
public interface IVerarbeitungsPlaner
{
    /// <param name="verzoegerung"><c>null</c> = sofort.</param>
    void Einplanen(Guid importVorgangId, TimeSpan? verzoegerung = null);
}
