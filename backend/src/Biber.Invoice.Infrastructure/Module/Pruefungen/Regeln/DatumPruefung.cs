using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Pruefungen.Regeln;

/// <summary>4. Das Rechnungsdatum liegt nicht in der Zukunft und ist nicht älter als <c>MaxAlterJahre</c>.</summary>
public sealed class DatumPruefung : IPruefung
{
    public PruefungArt Art => PruefungArt.Datum;

    public string Name => "Datum plausibel";

    public PruefBefundWert Pruefen(PruefEingabe e)
    {
        if (e.Beleg.Rechnungsdatum is not { } datum)
        {
            return PruefBefundWert.NichtAnwendbar("Kein Rechnungsdatum erfasst");
        }

        var jahre = e.Einstellungen.MaxAlterJahre;
        var alter = jahre == 1 ? "1 Jahr" : $"{jahre} Jahre";
        if (datum > e.Heute)
        {
            return PruefBefundWert.Fehler($"{PruefFormat.Datum(datum)} liegt in der Zukunft");
        }

        if (datum < e.Heute.AddYears(-jahre))
        {
            return PruefBefundWert.Fehler($"{PruefFormat.Datum(datum)} ist älter als {alter}");
        }

        return PruefBefundWert.Ok($"{PruefFormat.Datum(datum)} – nicht in der Zukunft, jünger als {alter}");
    }
}
