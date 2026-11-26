namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>
/// Ein Lauf „Alle Belege erneut auslesen“ (Einstellungen → Document Intelligence): liest jeden Beleg, der beim Start
/// vorhanden war, nacheinander neu aus. <see cref="Verarbeitet"/> ist zugleich der Stand, ab dem ein nach einem
/// Neustart fortgesetzter Lauf weitermacht.
/// </summary>
public class NeuAuslesenLauf
{
    public Guid Id { get; set; }
    public DateTimeOffset GestartetAm { get; set; }

    /// <summary>Zeitpunkt des zuletzt abgeschlossenen Belegs; ohne Fortschritt über längere Zeit gilt der Lauf als hängen geblieben.</summary>
    public DateTimeOffset AktualisiertAm { get; set; }

    public DateTimeOffset? BeendetAm { get; set; }

    /// <summary>Anzahl der Belege beim Start.</summary>
    public int Gesamt { get; set; }

    /// <summary>Abgearbeitete Belege, einschließlich Fehler und übersprungener.</summary>
    public int Verarbeitet { get; set; }

    public int AnzahlFehler { get; set; }

    /// <summary>Belege, die gerade in Verarbeitung waren oder inzwischen gelöscht sind.</summary>
    public int AnzahlUebersprungen { get; set; }

    /// <summary>Ein Admin hat den Lauf abgebrochen; der Job hört nach dem aktuellen Beleg auf.</summary>
    public bool AbbruchAngefordert { get; set; }

    /// <summary>Grund, aus dem der Lauf vorzeitig endete (z. B. Document-Intelligence-Limit).</summary>
    public string? Abbruchgrund { get; set; }

    public string? LetzterFehler { get; set; }

    /// <summary>Belege, deren Auslesen fehlschlug, als <c>jsonb</c> gespeichert.</summary>
    public List<Guid> FehlerBelege { get; set; } = [];
}
