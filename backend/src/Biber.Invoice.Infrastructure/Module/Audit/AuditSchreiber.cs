using Biber.Invoice.Domain.Module;

namespace Biber.Invoice.Infrastructure.Module.Audit;

/// <summary>Sammelt Notizen je Scope; der <see cref="AuditInterceptor"/> schreibt sie beim nächsten Speichern mit.</summary>
public sealed class AuditSchreiber : IAuditSchreiber
{
    private readonly List<Notiz> _notizen = [];

    public void Notieren(string entitaet, string entitaetId, Guid? belegId, string aktion, string? text = null) =>
        _notizen.Add(new Notiz(entitaet, entitaetId, belegId, aktion, text));

    internal IReadOnlyList<Notiz> Entnehmen()
    {
        var alle = _notizen.ToList();
        _notizen.Clear();
        return alle;
    }

    internal sealed record Notiz(string Entitaet, string EntitaetId, Guid? BelegId, string Aktion, string? Text);
}
