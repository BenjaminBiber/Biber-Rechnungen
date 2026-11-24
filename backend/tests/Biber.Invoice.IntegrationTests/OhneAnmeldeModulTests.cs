using System.Net;
using System.Net.Http.Headers;
using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests;

/// <summary>
/// Echte Anmeldung der Anwendung, ohne dass ein Anmelde-Modul die Schemata
/// <see cref="AuthSchemata.Cookie"/> und <see cref="AuthSchemata.ApiToken"/> registriert hat.
/// </summary>
[Collection("Api")]
public sealed class OhneAnmeldeModulTests(ApiFabrik basis)
{
    private readonly ApiFabrik _fabrik = basis
        .MitEchterAnmeldung()
        .MitDiensten(d => d.AddSingleton<IEndpunktModul, GeschuetzterEndpunkt>());

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Healthcheck_liefert_200(string pfad)
    {
        var client = _fabrik.ErzeugeAnonymenClient();

        var antwort = await client.GetAsync(pfad);

        antwort.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Geschuetzter_Endpunkt_liefert_401(bool mitBearer)
    {
        var client = _fabrik.ErzeugeAnonymenClient();
        if (mitBearer)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "irgendwas");
        }

        var antwort = await client.GetAsync("/api/test/ohne-anmelde-modul");

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class GeschuetzterEndpunkt : IEndpunktModul
    {
        public void Zuordnen(IEndpointRouteBuilder app) =>
            app.MapGet("/api/test/ohne-anmelde-modul", () => "ok").RequireAuthorization(Richtlinien.Lesen);
    }
}
