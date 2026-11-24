using Biber.Invoice.Api.Module;
using Biber.Invoice.Api.Module.Health;
using Biber.Invoice.Api.Sicherheit;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Infrastructure.Persistenz;
using Biber.Invoice.IntegrationTests.Infrastruktur;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests;

[Collection("Api")]
public sealed class ModulScanTests(ApiFabrik fabrik)
{
    [Fact]
    public void Scan_findet_Dienstmodule_in_Api_und_Infrastructure()
    {
        ModulScan.DienstModule.ShouldContain(typeof(HealthModul));
        ModulScan.DienstModule.ShouldContain(typeof(SicherheitsModul));
        ModulScan.DienstModule.ShouldContain(typeof(PersistenzModul));
    }

    [Fact]
    public void Endpunktmodule_sind_im_Container_registriert()
    {
        fabrik.Services.GetServices<IEndpunktModul>().ShouldContain(m => m is HealthModul);
    }

    [Fact]
    public void BiberDbContext_ist_aufloesbar()
    {
        using var scope = fabrik.ErzeugeScope();

        scope.ServiceProvider.GetService<BiberDbContext>().ShouldNotBeNull();
    }
}
