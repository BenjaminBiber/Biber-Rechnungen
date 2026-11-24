using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Logos;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SkiaSharp;

namespace Biber.Invoice.IntegrationTests.Logos;

[Collection("Api")]
public sealed class FaviconJobTests(ApiFabrik fabrik) : IAsyncLifetime
{
    // Korrespondenten anderer Testklassen würden sonst mit abgerufen.
    public Task InitializeAsync() => fabrik.ZuruecksetzenAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Setzt_Favicon_fuer_Korrespondenten_ohne_Logo()
    {
        var (f, favicons) = MitFavicons();
        var k = await f.SpeichernAsync(MitDomain("ohne-logo.de"));

        await JobAsync(f);

        await favicons.Received(1).AbrufenAsync("ohne-logo.de", Arg.Any<CancellationToken>());
        var gespeichert = await KorrespondentAsync(f, k.Id);
        gespeichert.LogoQuelle.ShouldBe(LogoQuelle.Favicon);
        gespeichert.LogoPfad.ShouldNotBeNull();
    }

    [Fact]
    public async Task Setzt_kein_Favicon_wenn_ein_Upload_existiert()
    {
        var (f, favicons) = MitFavicons();
        var k = MitDomain("mit-upload.de");
        await f.SpeichernAsync(k);
        using (var scope = f.ErzeugeScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
            var korrespondent = await db.Korrespondenten.SingleAsync(x => x.Id == k.Id);
            await scope.ServiceProvider.GetRequiredService<ILogoDienst>()
                .SetzenAsync(korrespondent, LogoTestbilder.Png(32, 32, SKColors.Blue), LogoQuelle.Upload, erzwingen: true, default);
        }

        await JobAsync(f);

        await favicons.DidNotReceive().AbrufenAsync("mit-upload.de", Arg.Any<CancellationToken>());
        (await KorrespondentAsync(f, k.Id)).LogoQuelle.ShouldBe(LogoQuelle.Upload);
    }

    [Fact]
    public async Task Versucht_eine_Domain_nicht_zweimal_in_30_Tagen()
    {
        var (f, favicons) = MitFavicons(bild: null);
        await f.SpeichernAsync(MitDomain("kein-icon.de"));

        await JobAsync(f);
        f.Zeit.Vorspulen(TimeSpan.FromDays(29));
        await JobAsync(f);

        await favicons.Received(1).AbrufenAsync("kein-icon.de", Arg.Any<CancellationToken>());

        f.Zeit.Vorspulen(TimeSpan.FromDays(2));
        await JobAsync(f);

        await favicons.Received(2).AbrufenAsync("kein-icon.de", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Hoechstens_20_Abrufe_je_Lauf()
    {
        var (f, favicons) = MitFavicons(bild: null);
        for (var i = 0; i < FaviconJob.MaxJeLauf + 5; i++)
        {
            await f.SpeichernAsync(MitDomain($"domain{i}.de"));
        }

        await JobAsync(f);

        await favicons.Received(FaviconJob.MaxJeLauf).AbrufenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Job_ist_als_wiederkehrender_Job_registriert()
    {
        using var scope = fabrik.ErzeugeScope();
        var job = scope.ServiceProvider.GetServices<IWiederkehrenderJob>().OfType<FaviconJob>().ShouldHaveSingleItem();
        job.Id.ShouldBe("favicons");
        job.Cron.ShouldBe("15 4 * * *");
    }

    private (ApiFabrik Fabrik, IFaviconAbruf Favicons) MitFavicons(bool mitBild = true) =>
        MitFavicons(mitBild ? LogoTestbilder.Png(32, 32, SKColors.Orange) : null);

    private (ApiFabrik Fabrik, IFaviconAbruf Favicons) MitFavicons(byte[]? bild)
    {
        var favicons = Substitute.For<IFaviconAbruf>();
        favicons.AbrufenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(bild);
        return (fabrik.MitDiensten(d => d.AddSingleton(favicons)), favicons);
    }

    private static Korrespondent MitDomain(string domain)
    {
        var k = Testdaten.Korrespondent();
        k.Domain = domain;
        return k;
    }

    private static async Task JobAsync(ApiFabrik f)
    {
        using var scope = f.ErzeugeScope();
        await scope.ServiceProvider.GetServices<IWiederkehrenderJob>().OfType<FaviconJob>().Single().AusfuehrenAsync(default);
    }

    private static async Task<Korrespondent> KorrespondentAsync(ApiFabrik f, Guid id)
    {
        using var scope = f.ErzeugeScope();
        return await scope.ServiceProvider.GetRequiredService<BiberDbContext>().Korrespondenten.AsNoTracking().SingleAsync(k => k.Id == id);
    }
}
