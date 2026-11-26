namespace Biber.Invoice.Domain.Entitaeten;

/// <summary>Kostenkategorie, optional einer EÜR-Zeile zugeordnet und optional Unterkategorie einer anderen.</summary>
public class Kategorie
{
    /// <summary>Trenner zwischen Ober- und Unterkategorie in Anzeige und Export.</summary>
    public const string Trenner = " › ";

    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Farbe als Hex-Wert, z. B. <c>#1F6FEB</c>.</summary>
    public string Farbe { get; set; } = "";

    public string? EuerZeile { get; set; }
    public Matching Matching { get; set; } = new();

    /// <summary>Oberkategorie; nur eine Ebene, eine Oberkategorie hat selbst keine.</summary>
    public Guid? ElternId { get; set; }
    public Kategorie? Eltern { get; set; }

    /// <summary>„Ober › Unter“, wenn <see cref="Eltern"/> geladen ist, sonst der Name.</summary>
    public string VollerName => Eltern is null ? Name : Eltern.Name + Trenner + Name;

    /// <summary>Eigene EÜR-Zeile, sonst die der Oberkategorie (wenn geladen).</summary>
    public string? WirksameEuerZeile =>
        !string.IsNullOrWhiteSpace(EuerZeile) ? EuerZeile
        : string.IsNullOrWhiteSpace(Eltern?.EuerZeile) ? null
        : Eltern.EuerZeile;
}
