using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace Biber.Invoice.Api.Hosting;

/// <summary>
/// Serilog-Ausgabe auf der Konsole. Standard ist eine lesbare Zeile pro Eintrag
/// (<c>2026-11-26 14:03:12 INF MailAbruf  3 neue Mails abgeholt</c>);
/// <c>Protokoll:Format = json</c> schaltet auf Compact-JSON für Log-Sammler um.
/// </summary>
public static class Protokollierung
{
    private const string Vorlage =
        "{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3} {Quelle,-24} {Message:lj}{NewLine}{Exception}";

    public static LoggerConfiguration KonsoleEinrichten(this LoggerConfiguration logger, IConfiguration konfiguration)
    {
        if (string.Equals(konfiguration["Protokoll:Format"], "json", StringComparison.OrdinalIgnoreCase))
        {
            return logger.WriteTo.Console(new RenderedCompactJsonFormatter());
        }

        return logger
            .Enrich.With<KurzeQuelle>()
            .WriteTo.Console(outputTemplate: Vorlage, theme: AnsiConsoleTheme.Code, formatProvider: System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
    }

    /// <summary>Kurze Zeile pro Anfrage: <c>GET /api/belege 200 (12 ms)</c>.</summary>
    public const string AnfrageVorlage = "{RequestMethod} {RequestPath} {StatusCode} ({Elapsed:0} ms)";

    /// <summary>Health-Checks und statische Dateien nur auf Debug, sonst fluten sie das Log.</summary>
    public static LogEventLevel AnfrageStufe(HttpContext kontext, double _, Exception? fehler)
    {
        if (fehler is not null || kontext.Response.StatusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        var pfad = kontext.Request.Path;
        var istRauschen = pfad.StartsWithSegments("/health")
            || (!pfad.StartsWithSegments("/api") && !pfad.StartsWithSegments("/mcp"));

        return istRauschen ? LogEventLevel.Debug : LogEventLevel.Information;
    }

    /// <summary>Kürzt <c>SourceContext</c> auf den Klassennamen, z. B. <c>Biber.Invoice.Infrastructure.Module.Mail.MailAbruf</c> → <c>MailAbruf</c>.</summary>
    private sealed class KurzeQuelle : ILogEventEnricher
    {
        public void Enrich(LogEvent eintrag, ILogEventPropertyFactory fabrik)
        {
            var quelle = eintrag.Properties.TryGetValue(Constants.SourceContextPropertyName, out var wert)
                && wert is ScalarValue { Value: string text }
                    ? text[(text.LastIndexOf('.') + 1)..]
                    : "-";

            if (quelle == "RequestLoggingMiddleware")
            {
                quelle = "Http";
            }

            eintrag.AddPropertyIfAbsent(fabrik.CreateProperty("Quelle", quelle));
        }
    }
}
