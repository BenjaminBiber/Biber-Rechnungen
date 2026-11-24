namespace Biber.Invoice.Infrastructure.Module.Aufbewahrung;

/// <summary>
/// Meldet Belege zur Löschung an. Nur was hier freigegeben ist, lässt <see cref="AufbewahrungsSchutz"/> löschen.
/// Gilt je Scope und wird ausschließlich von <see cref="AufbewahrungsDienst"/> benutzt.
/// </summary>
public interface IAufbewahrungsFreigabe
{
    void Freigeben(IEnumerable<Guid> belegIds);

    bool IstFreigegeben(Guid belegId);
}

internal sealed class AufbewahrungsFreigabe : IAufbewahrungsFreigabe
{
    private readonly HashSet<Guid> _freigegeben = [];

    public void Freigeben(IEnumerable<Guid> belegIds) => _freigegeben.UnionWith(belegIds);

    public bool IstFreigegeben(Guid belegId) => _freigegeben.Contains(belegId);
}
