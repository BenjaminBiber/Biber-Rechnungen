using Biber.Invoice.Infrastructure.Module.Kosit;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;

namespace Biber.Invoice.IntegrationTests.Kosit;

/// <summary>Baut das Image aus <c>docker/kosit</c> und startet es einmal für alle Tests dieser Klasse.</summary>
public sealed class KositSidecarFixture : IAsyncLifetime
{
    public static string KositOrdner { get; } = Ordner();

    private IFutureDockerImage? _image;
    private IContainer? _container;

    public KositClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(KositOrdner)
            .WithDockerfile("Dockerfile")
            .WithName($"biber-kosit-test:{Guid.NewGuid():N}")
            .WithCleanUp(true)
            .Build();
        await _image.CreateAsync();

        _container = new ContainerBuilder(_image)
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080).ForPath("/health")))
            .Build();
        await _container.StartAsync();

        var basis = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}/");
        Client = new KositClient(new HttpClient { BaseAddress = basis, Timeout = TimeSpan.FromSeconds(60) });
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        if (_image is not null)
        {
            await _image.DisposeAsync();
        }
    }

    private static string Ordner()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var kandidat = Path.Combine(dir.FullName, "docker", "kosit");
            if (File.Exists(Path.Combine(kandidat, "Dockerfile")))
            {
                return kandidat;
            }
        }

        throw new DirectoryNotFoundException("docker/kosit nicht gefunden.");
    }
}

[Trait("Kategorie", "Sidecar")]
public sealed class KositSidecarTests(KositSidecarFixture sidecar) : IClassFixture<KositSidecarFixture>
{
    private static byte[] Testdatei(string name) => File.ReadAllBytes(Path.Combine(KositSidecarFixture.KositOrdner, "test", name));

    [Fact]
    public async Task Gueltige_XRechnung_wird_akzeptiert()
    {
        var ergebnis = await sidecar.Client.ValidierenAsync(Testdatei("xrechnung-ubl.xml"), CancellationToken.None);

        ergebnis.Gueltig.ShouldBeTrue();
        ergebnis.Meldungen.ShouldBeEmpty();
    }

    [Fact]
    public async Task XRechnung_ohne_Kaeuferreferenz_wird_mit_BR_DE_15_abgelehnt()
    {
        var ergebnis = await sidecar.Client.ValidierenAsync(Testdatei("xrechnung-ohne-kaeuferreferenz.xml"), CancellationToken.None);

        ergebnis.Gueltig.ShouldBeFalse();
        ergebnis.Meldungen.ShouldContain(m => m.StartsWith("BR-DE-15 "));
    }

    [Fact]
    public async Task Visualisierung_enthaelt_die_Rechnungsnummer()
    {
        var html = await sidecar.Client.VisualisierenAsync(Testdatei("xrechnung-cii.xml"), CancellationToken.None);

        html.ShouldContain("123456XX");
    }
}
