using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.IntegrationTests.Infrastruktur;

public static class SystemTags
{
    /// <summary>
    /// Die Initial-Migration legt die System-Tags an; <see cref="ApiFabrik.ZuruecksetzenAsync"/> leert aber auch sie.
    /// Tests, die Belege durch die ganze Pipeline schicken, brauchen sie (die Prüfungen setzen „zu prüfen“).
    /// </summary>
    public static async Task SystemTagsSicherstellenAsync(this ApiFabrik fabrik)
    {
        using var scope = fabrik.ErzeugeScope();
        var db = scope.ServiceProvider.GetRequiredService<BiberDbContext>();
        if (!await db.Tags.AnyAsync(t => t.Id == SystemIds.TagInbox))
        {
            db.Tags.Add(new Tag { Id = SystemIds.TagInbox, Name = "Inbox", Farbe = "blau", IstInbox = true });
        }

        if (!await db.Tags.AnyAsync(t => t.Id == SystemIds.TagZuPruefen))
        {
            db.Tags.Add(new Tag { Id = SystemIds.TagZuPruefen, Name = "zu prüfen", Farbe = "orange", IstSystem = true });
        }

        await db.SaveChangesAsync();
    }
}
