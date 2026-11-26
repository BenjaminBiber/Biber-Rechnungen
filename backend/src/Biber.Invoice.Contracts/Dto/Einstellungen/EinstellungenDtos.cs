using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Contracts.Dto.Einstellungen;

/// <summary>Alle Einstellungsgruppen; geändert wird je Gruppe über <c>PUT einstellungen/{gruppe}</c>.</summary>
public sealed record EinstellungenDto(
    AllgemeinEinstellungen Allgemein,
    UstEinstellungen Ust,
    PruefEinstellungen Pruefungen,
    DiEinstellungen Di,
    MailEinstellungen Mail,
    OrdnerEinstellungen Ordner,
    AufbewahrungEinstellungen Aufbewahrung,
    ZugriffEinstellungen Zugriff,
    MailZugangDto MailZugang,
    UmgebungDto Umgebung);

public sealed record AllgemeinEinstellungen
{
    public string Firmenname { get; init; } = "";

    public string Standardwaehrung { get; init; } = "EUR";

    public string Zeitzone { get; init; } = "Europe/Berlin";

    public Guid InboxTagId { get; init; }
}

public sealed record UstEinstellungen
{
    public UstStatus Status { get; init; } = UstStatus.Unbekannt;

    public UstvaZeitraum Zeitraum { get; init; } = UstvaZeitraum.Quartalsweise;
}

public sealed record PruefEinstellungen
{
    public Dictionary<PruefungArt, bool> Aktiv { get; init; } = Enum.GetValues<PruefungArt>().ToDictionary(art => art, _ => true);

    public decimal SummenToleranz { get; init; } = 0.02m;

    public int MaxAlterJahre { get; init; } = 2;

    public decimal KleinbetragGrenze { get; init; } = 250m;

    public decimal KonfidenzSchwelle { get; init; } = 0.8m;
}

public sealed record DiEinstellungen
{
    public int Monatslimit { get; init; } = 500;

    public int WarnungAbProzent { get; init; } = 80;

    public int MaxSeitenJeBeleg { get; init; } = 10;
}

public sealed record MailEinstellungen
{
    public bool Aktiv { get; init; }

    public int AbrufintervallMinuten { get; init; } = 5;

    public string Eingangsordner { get; init; } = "INBOX";

    public string VerarbeitetOrdner { get; init; } = "Verarbeitet";

    public string OhneBelegOrdner { get; init; } = "Ohne Beleg";
}

public sealed record OrdnerEinstellungen
{
    public int PollingSekunden { get; init; } = 30;

    public int StabilSekunden { get; init; } = 10;
}

public sealed record AufbewahrungEinstellungen
{
    public int Jahre { get; init; } = 8;
}

public sealed record ZugriffEinstellungen
{
    public bool OffeneRegistrierung { get; init; }

    public bool GoogleAnmeldung { get; init; }
}

/// <summary>Nur lesbare Werte aus der Konfiguration (<c>.env</c>); enthält nie Schlüssel oder Passwörter.</summary>
public sealed record UmgebungDto(
    string AblagePfad,
    string EingangPfad,
    string? OeffentlicheUrl,
    string? DiEndpunkt,
    string? DiModell,
    bool DiKonfiguriert,
    bool GoogleKonfiguriert);

/// <summary>Document-Intelligence-Verbrauch des Monats (<c>yyyy-MM</c>).</summary>
public sealed record DiVerbrauchDto(string Monat, int Seiten, int Limit, int Prozent, bool Pausiert, int Wartend);

/// <summary>
/// IMAP-Zugang des Mail-Imports. <see cref="AusUmgebung"/>: per <c>.env</c> gesetzt (<c>IMAP_HOST</c>) und hier nicht änderbar,
/// sonst über <c>PUT einstellungen/mail/zugang</c> gepflegt. Das Passwort wird nie ausgeliefert.
/// </summary>
public sealed record MailZugangDto(
    bool AusUmgebung,
    string? Host,
    int Port,
    bool Ssl,
    string? Benutzer,
    bool PasswortGesetzt,
    bool Konfiguriert);

/// <summary>Neuer IMAP-Zugang; <see cref="Passwort"/> leer oder <c>null</c> behält das gespeicherte Passwort.</summary>
public sealed record MailZugangSpeichernDto(string Host, int Port, bool Ssl, string Benutzer, string? Passwort);

public sealed record MailStatusDto(bool Verbunden, DateTimeOffset? LetzterAbruf, string? Fehler);
