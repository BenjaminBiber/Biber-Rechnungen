using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Pipeline;

/// <summary>Zustand eines Verarbeitungslaufs, den die Schritte lesen und ergänzen.</summary>
public sealed class VerarbeitungsKontext
{
    public required ImportVorgang Vorgang { get; init; }
    public required Beleg Beleg { get; init; }

    /// <summary>Absoluter Pfad: Staging oder (bei Wiederholung) Original in der Ablage.</summary>
    public required string DateiPfad { get; init; }

    public required string Dateiname { get; init; }

    /// <summary>„Erneut auslesen“ eines bereits abgelegten Belegs.</summary>
    public bool IstWiederholung { get; init; }

    public ErkanntesFormat Format { get; set; }
    public byte[]? ERechnungXml { get; set; }
    public ExtraktionsErgebnis? Extraktion { get; set; }
    public KositErgebnis? Kosit { get; set; }
    public string? VisualisierungHtml { get; set; }
}
