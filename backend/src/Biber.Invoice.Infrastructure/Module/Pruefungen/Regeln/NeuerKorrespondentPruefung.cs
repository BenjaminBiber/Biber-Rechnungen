using System.Globalization;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>7. Der zugeordnete Korrespondent wurde von der Pipeline neu angelegt und noch nicht geprüft.</summary>
public sealed class NeuerKorrespondentPruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.NeuerKorrespondent;

    public string Name => "Neuer Korrespondent";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        if (e.Korrespondent is not { } k)
        {
            return PruefBefundWert.NichtAnwendbar("Kein Korrespondent zugeordnet");
        }

        if (e.KorrespondentIstNeu)
        {
            return PruefBefundWert.Fehler("Neuer Korrespondent");
        }

        var seit = k.ErstelltAm.UtcDateTime.ToString("MM/yyyy", CultureInfo.InvariantCulture);
        var belege = e.KorrespondentBelege == 1 ? "1 Beleg" : $"{e.KorrespondentBelege} Belege";
        return PruefBefundWert.Ok($"Bekannt seit {seit} · {belege}");
    }
}
