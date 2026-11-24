namespace Biber.Invoice.Domain.Pipeline;

public enum SchrittAusgang
{
    /// <summary>Weiter mit dem nächsten Schritt.</summary>
    Weiter,

    /// <summary>Die Pipeline endet regulär; die restlichen Schritte entfallen.</summary>
    Beendet,

    /// <summary>Vorübergehender Fehler; zählt als Versuch und wird mit Backoff wiederholt.</summary>
    SpaeterErneut,

    /// <summary>Der Vorgang ruht (z. B. DI-Limit erreicht); zählt nicht als Versuch.</summary>
    Pausiert,
}
