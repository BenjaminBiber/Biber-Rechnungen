namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>
/// Eine Hauptstelle aus der Bankleitzahlendatei der Deutschen Bundesbank. Über die Bankleitzahl (Stellen 5–12 einer
/// deutschen IBAN) bekommt eine Bankverbindung ihren Banknamen.
/// </summary>
public class Bank
{
    /// <summary>Achtstellige Bankleitzahl, z. B. <c>37040044</c>.</summary>
    public string Blz { get; set; } = "";

    /// <summary>Bezeichnung laut Bundesbank ohne den Zusatz „-alt-“, z. B. „Commerzbank“.</summary>
    public string Name { get; set; } = "";
}
