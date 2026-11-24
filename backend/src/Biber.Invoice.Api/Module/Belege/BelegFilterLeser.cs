using System.Globalization;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Belege.Lesen;

namespace Biber.Invoice.Api.Module.Belege;

/// <summary>
/// Liest <see cref="BelegFilterDto"/> aus dem Query-String. Listen kommen als wiederholte Parameter
/// (<c>tagIds=…&amp;tagIds=…</c>) oder kommagetrennt; Enums als Name. Fehlende Werte behalten den Standard des DTO.
/// </summary>
public static class BelegFilterLeser
{
    public static BelegFilterDto Lesen(IQueryCollection query, Dictionary<string, string[]> fehler)
    {
        var standard = new BelegFilterDto();
        var filter = new BelegFilterDto
        {
            Suche = Text(query, "suche"),
            Suchfeld = EnumWert(query, "suchfeld", standard.Suchfeld, fehler),
            Ansicht = EnumWert(query, "ansicht", standard.Ansicht, fehler),
            TagIds = Ids(query, "tagIds", fehler),
            KorrespondentIds = Ids(query, "korrespondentIds", fehler),
            KategorieIds = Ids(query, "kategorieIds", fehler),
            Von = Datum(query, "von", fehler),
            Bis = Datum(query, "bis", fehler),
            BetragVon = Betrag(query, "betragVon", fehler),
            BetragBis = Betrag(query, "betragBis", fehler),
            BetragArt = EnumWert(query, "betragArt", standard.BetragArt, fehler),
            Zahlstatus = Text(query, "zahlstatus") is null ? null : EnumWert(query, "zahlstatus", Zahlstatus.Offen, fehler),
            Quellen = [.. Werte(query, "quellen").Select(w => EnumAus<ImportQuelle>("quellen", w, fehler)).OfType<ImportQuelle>()],
            DublettenAusschliessen = Bool(query, "dublettenAusschliessen", standard.DublettenAusschliessen, fehler),
            Sortierung = EnumWert(query, "sortierung", standard.Sortierung, fehler),
            Absteigend = Bool(query, "absteigend", standard.Absteigend, fehler),
            Seite = Zahl(query, "seite", standard.Seite, fehler),
            Seitengroesse = Zahl(query, "seitengroesse", standard.Seitengroesse, fehler),
        };

        if (filter.Seite < 1)
        {
            fehler.TryAdd("seite", ["Die Seite muss mindestens 1 sein."]);
        }

        if (filter.Seitengroesse is < BelegAbfrageDienst.MinSeitengroesse or > BelegAbfrageDienst.MaxSeitengroesse)
        {
            fehler.TryAdd("seitengroesse", [$"Die Seitengröße muss zwischen {BelegAbfrageDienst.MinSeitengroesse} und {BelegAbfrageDienst.MaxSeitengroesse} liegen."]);
        }

        if (filter.Von is { } von && filter.Bis is { } bis && von > bis)
        {
            fehler.TryAdd("bis", ["„Bis“ darf nicht vor „Von“ liegen."]);
        }

        return filter;
    }

    private static string? Text(IQueryCollection query, string name) =>
        query.TryGetValue(name, out var werte) && werte.LastOrDefault() is { Length: > 0 } wert ? wert : null;

    private static IEnumerable<string> Werte(IQueryCollection query, string name) =>
        query.TryGetValue(name, out var werte)
            ? werte.SelectMany(w => (w ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            : [];

    private static Guid[] Ids(IQueryCollection query, string name, Dictionary<string, string[]> fehler)
    {
        List<Guid> ids = [];
        foreach (var wert in Werte(query, name))
        {
            if (Guid.TryParse(wert, out var id))
            {
                ids.Add(id);
            }
            else
            {
                fehler.TryAdd(name, [$"„{wert}“ ist keine gültige ID."]);
            }
        }

        return [.. ids.Distinct()];
    }

    private static DateOnly? Datum(IQueryCollection query, string name, Dictionary<string, string[]> fehler)
    {
        if (Text(query, name) is not { } wert)
        {
            return null;
        }

        if (DateOnly.TryParseExact(wert, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum))
        {
            return datum;
        }

        fehler.TryAdd(name, ["Das Datum muss im Format JJJJ-MM-TT angegeben werden."]);
        return null;
    }

    private static decimal? Betrag(IQueryCollection query, string name, Dictionary<string, string[]> fehler)
    {
        if (Text(query, name) is not { } wert)
        {
            return null;
        }

        if (decimal.TryParse(wert, NumberStyles.Number, CultureInfo.InvariantCulture, out var betrag))
        {
            return betrag;
        }

        fehler.TryAdd(name, ["Der Betrag muss eine Zahl mit Punkt als Dezimaltrennzeichen sein."]);
        return null;
    }

    private static bool Bool(IQueryCollection query, string name, bool standard, Dictionary<string, string[]> fehler)
    {
        if (Text(query, name) is not { } wert)
        {
            return standard;
        }

        if (bool.TryParse(wert, out var ergebnis))
        {
            return ergebnis;
        }

        fehler.TryAdd(name, ["Erlaubt sind „true“ und „false“."]);
        return standard;
    }

    private static int Zahl(IQueryCollection query, string name, int standard, Dictionary<string, string[]> fehler)
    {
        if (Text(query, name) is not { } wert)
        {
            return standard;
        }

        if (int.TryParse(wert, NumberStyles.Integer, CultureInfo.InvariantCulture, out var zahl))
        {
            return zahl;
        }

        fehler.TryAdd(name, ["Der Wert muss eine ganze Zahl sein."]);
        return standard;
    }

    private static T EnumWert<T>(IQueryCollection query, string name, T standard, Dictionary<string, string[]> fehler)
        where T : struct, Enum =>
        Text(query, name) is { } wert ? EnumAus<T>(name, wert, fehler) ?? standard : standard;

    private static T? EnumAus<T>(string name, string wert, Dictionary<string, string[]> fehler)
        where T : struct, Enum
    {
        // Nur einzelne Namen: Zahlen und kommagetrennte Kombinationen würde Enum.TryParse sonst auch annehmen.
        if (!int.TryParse(wert, out _) && !wert.Contains(',') && Enum.TryParse<T>(wert, ignoreCase: true, out var ergebnis) && Enum.IsDefined(ergebnis))
        {
            return ergebnis;
        }

        fehler.TryAdd(name, [$"Erlaubt sind {string.Join(", ", Enum.GetNames<T>().Select(n => $"„{n}“"))}."]);
        return null;
    }
}
