using System.Net;
using Biber.Invoice.Infrastructure.Module.Logos;
using SkiaSharp;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Biber.Invoice.IntegrationTests.Logos;

/// <summary>Favicon-Abruf gegen einen lokalen WireMock-Server; kein Test geht ins Internet.</summary>
public sealed class FaviconAbrufTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    [Fact]
    public async Task Liest_link_rel_icon_und_bevorzugt_das_groesste_Icon()
    {
        Html("""
             <html><head>
               <link rel="icon" href="/klein.png" sizes="16x16">
               <link rel='icon' type="image/png" href="/gross.png" sizes="64x64">
             </head><body></body></html>
             """);
        Datei("/klein.png", LogoTestbilder.Png(16, 16, SKColors.Blue));
        Datei("/gross.png", LogoTestbilder.Png(64, 64, SKColors.Red));

        var png = await Abruf().AbrufenAsync("beispiel.de", default);

        png.ShouldNotBeNull();
        using var bild = LogoTestbilder.Lesen(png);
        bild.Width.ShouldBe(64);
        bild.GetPixel(32, 32).Red.ShouldBeGreaterThan((byte)200);
    }

    [Fact]
    public async Task Wandelt_ICO_aus_shortcut_icon_nach_PNG()
    {
        Html("""<html><head><link rel="shortcut icon" href="favicon-32.ico"></head></html>""");
        Datei("/favicon-32.ico", LogoTestbilder.Ico(LogoTestbilder.Png(32, 32, SKColors.Green), 32));

        var png = await Abruf().AbrufenAsync("beispiel.de", default);

        png.ShouldNotBeNull();
        LogoBild.Format(png).ShouldBe(SKEncodedImageFormat.Png);
        using var bild = LogoTestbilder.Lesen(png);
        bild.Width.ShouldBe(32);
    }

    [Fact]
    public async Task Ohne_link_wird_favicon_ico_genommen()
    {
        Html("<html><head><title>Ohne Icon</title></head></html>");
        Datei("/favicon.ico", LogoTestbilder.Ico(LogoTestbilder.Png(48, 48, SKColors.Purple), 48));

        var png = await Abruf().AbrufenAsync("beispiel.de", default);

        png.ShouldNotBeNull();
    }

    [Fact]
    public async Task Kein_Favicon_ergibt_null()
    {
        Html("<html></html>");

        (await Abruf().AbrufenAsync("beispiel.de", default)).ShouldBeNull();
    }

    [Fact]
    public async Task Mehr_als_drei_Weiterleitungen_werden_nicht_verfolgt()
    {
        for (var i = 0; i < 5; i++)
        {
            _server.Given(Request.Create().WithPath($"/w{i}").UsingGet())
                .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", $"/w{i + 1}"));
        }

        _server.Given(Request.Create().WithPath("/w5").UsingGet())
            .RespondWith(Response.Create().WithBody(LogoTestbilder.Png(16, 16, SKColors.Red)));
        _server.Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", "/w0"));
        _server.Given(Request.Create().WithPath("/favicon.ico").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", "/w1"));

        (await Abruf().AbrufenAsync("beispiel.de", default)).ShouldBeNull();
    }

    [Fact]
    public async Task Zu_grosses_Icon_wird_verworfen()
    {
        Html("<html></html>");
        var gross = new byte[FaviconAbruf.MaxGroesse + 10];
        LogoTestbilder.Png(16, 16, SKColors.Red).CopyTo(gross, 0);
        Datei("/favicon.ico", gross);

        (await Abruf().AbrufenAsync("beispiel.de", default)).ShouldBeNull();
    }

    [Fact]
    public async Task Lokaler_Server_wird_mit_der_Standardpruefung_nicht_erreicht()
    {
        Html("<html></html>");
        Datei("/favicon.ico", LogoTestbilder.Png(16, 16, SKColors.Red));
        using var abruf = new FaviconAbruf(basisadresse: _ => new Uri(_server.Url!));

        (await abruf.AbrufenAsync("beispiel.de", default)).ShouldBeNull();
        _server.LogEntries.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("10.0.0.5")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]
    [InlineData("[::1]")]
    public async Task Private_Adressen_werden_abgelehnt(string domain)
    {
        using var abruf = new FaviconAbruf();

        (await abruf.AbrufenAsync(domain, default)).ShouldBeNull();
    }

    [Theory]
    [InlineData("10.0.0.5", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.32.0.1", true)]
    [InlineData("192.168.0.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:10.0.0.5", false)]
    [InlineData("93.184.216.34", true)]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946", true)]
    public void IstOeffentlich_erkennt_private_Bereiche(string adresse, bool erwartet) =>
        FaviconAbruf.IstOeffentlich(IPAddress.Parse(adresse)).ShouldBe(erwartet);

    /// <summary>Abruf gegen WireMock; die Adressprüfung ist hier aus, weil WireMock auf Loopback lauscht.</summary>
    private FaviconAbruf Abruf() => new(basisadresse: _ => new Uri(_server.Url!), adresseErlaubt: _ => true);

    private void Html(string html) =>
        _server.Given(Request.Create().WithPath("/").UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "text/html").WithBody(html));

    private void Datei(string pfad, byte[] inhalt) =>
        _server.Given(Request.Create().WithPath(pfad).UsingGet())
            .RespondWith(Response.Create().WithHeader("Content-Type", "image/png").WithBody(inhalt));
}
