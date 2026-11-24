using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;

namespace Biber.Invoice.Infrastructure.Module.Belege.Lesen;

/// <summary>
/// Fachbegriffe der Belegliste als wiederverwendbare Filter. Bezahlt = mindestens ein Zahlungs-Tag; offen = kein
/// Zahlungs-Tag; überfällig = offen und Fälligkeit vor heute. Dubletten zählen weder als offen noch als überfällig.
/// </summary>
public static class BelegAbfragen
{
    /// <summary>Belege, die die Liste überhaupt zeigt: fertig verarbeitet oder mit Fehler abgebrochen.</summary>
    public static IQueryable<Beleg> Grundmenge(this IQueryable<Beleg> belege) =>
        belege.Where(b => b.Status == BelegStatus.Fertig || b.Status == BelegStatus.Fehler);

    public static IQueryable<Beleg> OhneDubletten(this IQueryable<Beleg> belege) =>
        belege.Where(b => b.DuplikatVonId == null);

    public static IQueryable<Beleg> NurBezahlte(this IQueryable<Beleg> belege) =>
        belege.Where(b => b.Tags.Any(t => t.Tag!.IstZahlungsTag));

    public static IQueryable<Beleg> NurOffene(this IQueryable<Beleg> belege) =>
        belege.Where(b => b.DuplikatVonId == null && !b.Tags.Any(t => t.Tag!.IstZahlungsTag));

    public static IQueryable<Beleg> NurUeberfaellige(this IQueryable<Beleg> belege, DateOnly heute) =>
        belege.NurOffene().Where(b => b.Faelligkeitsdatum < heute);

    public static IQueryable<Beleg> NurInbox(this IQueryable<Beleg> belege) =>
        belege.Where(b => b.Tags.Any(t => t.Tag!.IstInbox));

    public static IQueryable<Beleg> NurZuPruefen(this IQueryable<Beleg> belege) =>
        belege.Where(b => b.Tags.Any(t => t.TagId == SystemIds.TagZuPruefen));

    public static IQueryable<Beleg> MitAnsicht(this IQueryable<Beleg> belege, BelegAnsicht ansicht, DateOnly heute) => ansicht switch
    {
        BelegAnsicht.Inbox => belege.NurInbox(),
        BelegAnsicht.ZuPruefen => belege.NurZuPruefen(),
        BelegAnsicht.Offen => belege.NurOffene(),
        BelegAnsicht.Ueberfaellig => belege.NurUeberfaellige(heute),
        _ => belege,
    };

    public static IQueryable<Beleg> MitZahlstatus(this IQueryable<Beleg> belege, Zahlstatus? status, DateOnly heute) => status switch
    {
        Zahlstatus.Offen => belege.NurOffene(),
        Zahlstatus.Bezahlt => belege.NurBezahlte(),
        Zahlstatus.Ueberfaellig => belege.NurUeberfaellige(heute),
        _ => belege,
    };
}
