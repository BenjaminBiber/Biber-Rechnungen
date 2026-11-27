using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>
/// Stand der Tabelle <see cref="Bank"/> – genau eine Zeile (<see cref="EinzigeId"/>): woher und wann die
/// Bankleitzahlen zuletzt geladen wurden und wie der letzte automatische Abruf ausging.
/// </summary>
public class BankleitzahlenStand
{
    public const int EinzigeId = 1;

    public int Id { get; set; } = EinzigeId;

    /// <summary>Anzahl der Banken in der Tabelle.</summary>
    public int Anzahl { get; set; }

    public DateTimeOffset? GeladenAm { get; set; }

    public BankleitzahlenQuelle? Quelle { get; set; }

    /// <summary>Adresse der abgerufenen Datei bzw. Name der hochgeladenen; ändert sich die Adresse nicht, wird nicht neu geladen.</summary>
    public string? Datei { get; set; }

    /// <summary>Letzter Versuch, die Datei von der Bundesbank zu holen – erfolgreich oder nicht.</summary>
    public DateTimeOffset? LetzterAbrufAm { get; set; }

    /// <summary>Fehler des letzten Abrufs; <c>null</c>, wenn er geklappt hat.</summary>
    public string? LetzterFehler { get; set; }
}
