using System.Security.Cryptography;
using System.Text;
using Biber.Invoice.Infrastructure.Module.Kosit;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Configurations;
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
        // Das Tag folgt dem Inhalt von docker/kosit: Docker baut nur bei Änderungen neu, sonst gilt das vorhandene Image.
        // Ein neues Zufalls-Tag je Lauf hat das Image jedes Mal neu gebaut; unter Last blieb dieser Bau hängen.
        _image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(KositOrdner)
            .WithDockerfile("Dockerfile")
            .WithName($"biber-kosit-test:{InhaltsHash()}")
            .WithImageBuildPolicy(PullPolicy.Missing)
            .WithCleanUp(false)
            .Build();
        using (var bauen = new CancellationTokenSource(TimeSpan.FromMinutes(15)))
        {
            await _image.CreateAsync(bauen.Token);
        }

        _container = new ContainerBuilder(_image)
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                r => r.ForPort(8080).ForPath("/health"), o => o.WithTimeout(TimeSpan.FromMinutes(3))))
            .Build();
        try
        {
            await _container.StartAsync();
        }
        catch (Exception e)
        {
            var (stdout, stderr) = await _container.GetLogsAsync();
            throw new InvalidOperationException($"KoSIT-Sidecar startet nicht.\n{stdout}\n{stderr}", e);
        }

        var basis = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(8080)}/");
        Client = new KositClient(new HttpClient { BaseAddress = basis, Timeout = TimeSpan.FromSeconds(60) });
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static string InhaltsHash()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var datei in Directory.EnumerateFiles(KositOrdner, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(KositOrdner, datei).Replace('\\', '/')));
            hash.AppendData(File.ReadAllBytes(datei));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset())[..16];
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
