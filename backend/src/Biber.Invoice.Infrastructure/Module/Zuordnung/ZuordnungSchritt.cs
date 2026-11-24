using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Pipeline;

namespace Biber.Invoice.Infrastructure.Module.Zuordnung;

/// <summary>Schritt 7: Kategorie und Tags über Default-Kategorie und Matching-Regeln.</summary>
public sealed class ZuordnungSchritt(IZuordnungsDienst zuordnung) : IPipelineSchritt
{
    public int Reihenfolge => SchrittReihenfolge.Zuordnung;

    public string Name => "Zuordnung";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        await zuordnung.ZuordnenAsync(kontext.Beleg, ct);
        return SchrittErgebnis.Weiter;
    }
}
