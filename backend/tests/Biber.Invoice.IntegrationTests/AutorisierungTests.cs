using System.Net;
using Biber.Invoice.Contracts;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests;

[Collection("Api")]
public sealed class AutorisierungTests(ApiFabrik basis)
{
    private readonly ApiFabrik _fabrik =
        basis.MitDiensten(d => d.AddSingleton<IEndpunktModul, RichtlinienEndpunkte>());

    [Theory]
    [InlineData(Rolle.Admin, Richtlinien.Lesen, HttpStatusCode.OK)]
    [InlineData(Rolle.Admin, Richtlinien.Bearbeiten, HttpStatusCode.OK)]
    [InlineData(Rolle.Admin, Richtlinien.Admin, HttpStatusCode.OK)]
    [InlineData(Rolle.Benutzer, Richtlinien.Lesen, HttpStatusCode.OK)]
    [InlineData(Rolle.Benutzer, Richtlinien.Bearbeiten, HttpStatusCode.OK)]
    [InlineData(Rolle.Benutzer, Richtlinien.Admin, HttpStatusCode.Forbidden)]
    [InlineData(Rolle.NurLesen, Richtlinien.Lesen, HttpStatusCode.OK)]
    [InlineData(Rolle.NurLesen, Richtlinien.Bearbeiten, HttpStatusCode.Forbidden)]
    [InlineData(Rolle.NurLesen, Richtlinien.Admin, HttpStatusCode.Forbidden)]
    public async Task Richtlinie_laesst_nur_passende_Rollen_zu(Rolle rolle, string richtlinie, HttpStatusCode erwartet)
    {
        var client = _fabrik.ErzeugeClient(rolle);

        var antwort = await client.GetAsync($"/api/test/{richtlinie}");

        antwort.StatusCode.ShouldBe(erwartet);
    }

    [Theory]
    [InlineData(Richtlinien.Lesen)]
    [InlineData(Richtlinien.Bearbeiten)]
    [InlineData(Richtlinien.Admin)]
    public async Task Anonym_liefert_401(string richtlinie)
    {
        var client = _fabrik.ErzeugeAnonymenClient();

        var antwort = await client.GetAsync($"/api/test/{richtlinie}");

        antwort.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class RichtlinienEndpunkte : IEndpunktModul
    {
        public void Zuordnen(IEndpointRouteBuilder app)
        {
            foreach (var richtlinie in new[] { Richtlinien.Lesen, Richtlinien.Bearbeiten, Richtlinien.Admin })
            {
                app.MapGet($"/api/test/{richtlinie}", () => "ok").RequireAuthorization(richtlinie);
            }
        }
    }
}
