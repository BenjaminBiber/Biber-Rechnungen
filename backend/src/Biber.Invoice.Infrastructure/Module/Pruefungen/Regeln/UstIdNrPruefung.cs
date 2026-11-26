using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>
/// 11. Die USt-IdNr. des Korrespondenten ist laut VIES gültig. Geprüft wird im Hintergrund; bis dahin ist der Befund
/// „nicht anwendbar“. Eine ungültige Nummer kann auf eine gefälschte Rechnung oder eine erloschene Firma hindeuten.
/// </summary>
public sealed class UstIdNrPruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.UstIdNr;

    public string Name => "USt-IdNr. gültig (VIES)";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        if (e.Beleg.Richtung != Richtung.Eingang)
        {
            return PruefBefundWert.NichtAnwendbar("Nur für Eingangsbelege");
        }

        if (e.Korrespondent is not { } k)
        {
            return PruefBefundWert.NichtAnwendbar("Kein Korrespondent zugeordnet");
        }

        if (k.UstIdNr is null)
        {
            return PruefBefundWert.NichtAnwendbar("Beim Korrespondenten ist keine USt-IdNr. hinterlegt");
        }

        var datum = k.UstIdGeprueftAm is { } am ? $" (geprüft am {am:dd.MM.yyyy})" : "";
        return k.AktuellerUstIdStatus() switch
        {
            UstIdStatus.Gueltig => PruefBefundWert.Ok($"USt-IdNr. {k.UstIdNr} ist laut VIES gültig{datum}"),
            UstIdStatus.Ungueltig => PruefBefundWert.Fehler($"USt-IdNr. {k.UstIdNr} ist laut VIES nicht gültig{datum}"),
            UstIdStatus.NichtPruefbar => PruefBefundWert.NichtAnwendbar($"USt-IdNr. {k.UstIdNr} gehört zu keinem EU-Mitgliedstaat"),
            UstIdStatus.NichtErreichbar => PruefBefundWert.NichtAnwendbar("VIES war nicht erreichbar; die Prüfung wird wiederholt"),
            _ => PruefBefundWert.NichtAnwendbar("Die USt-IdNr. wurde noch nicht online geprüft"),
        };
    }
}
