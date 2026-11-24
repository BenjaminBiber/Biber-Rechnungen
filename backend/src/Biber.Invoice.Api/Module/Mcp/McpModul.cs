using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Module.Verfahrensdoku;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Biber.Invoice.Api.Module.Mcp;

/// <summary>MCP-Server „biber-rechnungen“ unter <c>/mcp</c> (Streamable HTTP), nur mit API-Token und Scope „lesen“.</summary>
public sealed class McpModul : IDienstModul, IEndpunktModul
{
    public const string Pfad = "/mcp";
    public const string ServerName = "biber-rechnungen";

    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.AddHttpContextAccessor();
        dienste.AddMcpServer()
            .WithHttpTransport()
            .WithTools<BelegTools>()
            .WithTools<AuswertungTools>()
            .WithTools<KorrespondentTools>()
            .WithTools<ArtikelTools>()
            .WithTools<UploadTools>();

        // Version erst beim Start lesen: SoftwareVersion registriert ein anderes Modul.
        dienste.AddSingleton<IConfigureOptions<McpServerOptions>>(sp => new ConfigureOptions<McpServerOptions>(o =>
            o.ServerInfo = new Implementation
            {
                Name = ServerName,
                Version = sp.GetService<SoftwareVersion>()?.Wert ?? "unbekannt",
            }));
    }

    public void Zuordnen(IEndpointRouteBuilder app) =>
        app.MapMcp(Pfad).RequireAuthorization(McpRichtlinien.McpLesen);
}
