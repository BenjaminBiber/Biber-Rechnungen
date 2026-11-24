namespace Biber.Invoice.Infrastructure.Module.Auth;

/// <summary>Kurzform eines User-Agents für die Sitzungsliste, z. B. „Firefox · Windows“.</summary>
public static class Geraet
{
    private static readonly (string Merkmal, string Name)[] Browser =
    [
        ("Edg/", "Edge"),
        ("OPR/", "Opera"),
        ("Firefox/", "Firefox"),
        ("Chrome/", "Chrome"),
        ("Safari/", "Safari"),
    ];

    private static readonly (string Merkmal, string Name)[] Systeme =
    [
        ("Windows", "Windows"),
        ("Android", "Android"),
        ("iPhone", "iOS"),
        ("iPad", "iOS"),
        ("Mac OS", "macOS"),
        ("Linux", "Linux"),
    ];

    public static string Kurzform(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "Unbekannt";
        }

        var browser = Browser.FirstOrDefault(b => userAgent.Contains(b.Merkmal, StringComparison.Ordinal)).Name;
        var system = Systeme.FirstOrDefault(s => userAgent.Contains(s.Merkmal, StringComparison.Ordinal)).Name;
        return (browser, system) switch
        {
            (null, null) => userAgent.Length > 40 ? userAgent[..40] : userAgent,
            (null, _) => system,
            (_, null) => browser,
            _ => $"{browser} · {system}",
        };
    }
}
