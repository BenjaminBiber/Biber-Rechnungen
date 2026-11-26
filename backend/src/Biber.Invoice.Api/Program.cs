using Biber.Invoice.Api.Hosting;
using Biber.Invoice.Api.Module;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((dienste, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(dienste)
    .Enrich.FromLogContext()
    .KonsoleEinrichten(builder.Configuration));

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.RegistriereModule(builder.Configuration);

var app = builder.Build();

if (app.Configuration.GetValue("Datenbank:MigrationBeimStart", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(o =>
{
    o.MessageTemplate = Protokollierung.AnfrageVorlage;
    o.GetLevel = Protokollierung.AnfrageStufe;
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.OrdneModuleZu();

// SPA-Fallback: alles außer /api, /mcp und /health liefert index.html des Frontends.
foreach (var praefix in new[] { "api", "mcp", "health" })
{
    app.MapFallback($"/{praefix}/{{**pfad}}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound));
}

app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
