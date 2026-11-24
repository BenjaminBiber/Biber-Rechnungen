using Biber.Invoice.Contracts.Dienste;
using Biber.Invoice.Domain.Module;
using Biber.Invoice.Domain.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Biber.Invoice.Infrastructure.Module.Mail;

/// <summary>Mail-Import per IMAP: Abruf, minütlicher Job und Status-Quelle. Zugang aus <c>Imap:*</c>.</summary>
public sealed class MailModul : IDienstModul
{
    public void Registrieren(IServiceCollection dienste, IConfiguration konfiguration)
    {
        dienste.Configure<ImapZugang>(konfiguration.GetSection("Imap"));
        dienste.AddSingleton<MailAbruf>();
        dienste.AddSingleton<IWiederkehrenderJob, MailAbrufJob>();
        dienste.AddSingleton<ISystemStatusQuelle, PostfachStatus>();
    }
}
