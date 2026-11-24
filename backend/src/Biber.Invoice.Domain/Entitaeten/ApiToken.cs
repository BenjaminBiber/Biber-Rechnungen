namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Persönliches API-Token (MCP, Skripte). Gespeichert wird nur der Hash.</summary>
public class ApiToken
{
    public Guid Id { get; set; }
    public Guid BenutzerId { get; set; }
    public string Name { get; set; } = "";
    public string Hash { get; set; } = "";

    /// <summary>Letzte 4 Zeichen des Tokens, zur Anzeige.</summary>
    public string Endung { get; set; } = "";

    /// <summary>Berechtigungen: <c>lesen</c>, <c>upload</c>.</summary>
    public List<string> Scopes { get; set; } = [];

    public DateTimeOffset ErstelltAm { get; set; }
    public DateTimeOffset? ZuletztGenutztAm { get; set; }
    public DateTimeOffset? WiderrufenAm { get; set; }
}
