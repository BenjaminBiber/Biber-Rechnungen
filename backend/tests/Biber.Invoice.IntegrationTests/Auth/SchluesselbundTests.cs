using System.Net;
using System.Net.Http.Json;
using Biber.Invoice.Contracts;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Infrastructure.Module.Auth;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Biber.Invoice.IntegrationTests.Auth;

/// <summary>
/// Der Schlüsselbund von Data Protection liegt unter <c>Pfade:Daten/schluessel</c>: Ein Anmelde-Cookie überlebt
/// den Neustart der Anwendung, solange der Daten-Ordner erhalten bleibt. Ein zweiter Host mit eigener Fabrik
/// bildet den Neustart nach (eigener Dienstebaum, eigener Schlüssel-Cache, dieselbe Datenbank).
/// </summary>
[Collection("Api")]
public sealed class SchluesselbundTests(ApiFabrik basis) : IAsyncLifetime
{
    private readonly ApiFabrik _fabrik = basis.MitEchterAnmeldung();

    public Task InitializeAsync() => _fabrik.VorbereitenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Cookie_gilt_nach_Neustart_mit_demselben_Daten_Ordner()
    {
        var cookie = await AngemeldetenCookieAsync();

        var neustart = _fabrik.MitDatenOrdner(_fabrik.DatenOrdner);
        neustart.Zeit.Setze(_fabrik.Zeit.GetUtcNow());

        (await EigenesAsync(neustart, cookie)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Cookie_gilt_nicht_mit_anderem_Daten_Ordner()
    {
        var cookie = await AngemeldetenCookieAsync();

        var anderer = _fabrik.MitDatenOrdner(Path.Combine(_fabrik.TempOrdner, "anderer-daten-ordner"));
        anderer.Zeit.Setze(_fabrik.Zeit.GetUtcNow());

        (await EigenesAsync(anderer, cookie)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Schluesseldatei_liegt_nach_Anmeldung_im_Daten_Ordner()
    {
        await AngemeldetenCookieAsync();

        var ordner = Path.Combine(_fabrik.DatenOrdner, "schluessel");
        Directory.Exists(ordner).ShouldBeTrue();
        Directory.EnumerateFiles(ordner, "key-*.xml").ShouldNotBeEmpty();
    }

    /// <summary>Meldet einen Benutzer an der Fabrik an und liefert das Sitzungs-Cookie als „Name=Wert“.</summary>
    private async Task<string> AngemeldetenCookieAsync()
    {
        var benutzer = await _fabrik.BenutzerAnlegenAsync(Rolle.Benutzer);
        var antwort = await _fabrik.Client().PostAsJsonAsync(
            ApiRouten.Auth.Anmelden, new AnmeldenDto(benutzer.Email!, AuthTestHilfen.Passwort, false), AuthTestHilfen.Json);
        antwort.EnsureSuccessStatusCode();

        return antwort.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith(AuthModul.CookieName + "=", StringComparison.Ordinal))
            .Split(';')[0];
    }

    private static async Task<HttpStatusCode> EigenesAsync(ApiFabrik fabrik, string cookie)
    {
        var client = fabrik.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return (await client.GetAsync(ApiRouten.Konto.Eigenes)).StatusCode;
    }
}
