using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Infrastructure.Module.Korrespondenten;
using Biber.Invoice.Infrastructure.Module.KorrespondentZuordnung.Domain.Regeln;
using Biber.Invoice.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Biber.Invoice.Infrastructure.Module.KorrespondentZuordnung;

/// <summary>
/// Schritt 4: Ordnet dem Beleg den Aussteller als Korrespondenten zu – Rangfolge USt-IdNr., IBAN, Name/Alias,
/// Matching-Regel – und legt sonst einen neuen, ungeprüften Korrespondenten an. Beim Treffer werden fehlende
/// Angaben ergänzt, nie überschrieben.
/// </summary>
public sealed class KorrespondentZuordnungSchritt(BiberDbContext db, TimeProvider zeit) : IPipelineSchritt
{
    private static readonly HashSet<string> FreemailDomains =
    [
        "gmail.com", "googlemail.com", "gmx.de", "gmx.net", "gmx.at", "web.de", "outlook.com", "outlook.de",
        "hotmail.com", "hotmail.de", "live.com", "live.de", "t-online.de", "yahoo.com", "yahoo.de", "icloud.com",
        "me.com", "aol.com", "freenet.de", "posteo.de", "mailbox.org", "proton.me", "protonmail.com",
    ];

    public int Reihenfolge => SchrittReihenfolge.Korrespondent;

    public string Name => "Korrespondent";

    public async Task<SchrittErgebnis> AusfuehrenAsync(VerarbeitungsKontext kontext, CancellationToken ct)
    {
        var beleg = kontext.Beleg;
        if (beleg.KorrespondentId is not null && beleg.KorrespondentErkennung == KorrespondentErkennung.Manuell)
        {
            return SchrittErgebnis.Weiter;
        }

        var aussteller = kontext.Extraktion?.Aussteller;
        if (aussteller is null || !HatAngaben(aussteller))
        {
            return SchrittErgebnis.Weiter;
        }

        var alle = await db.Korrespondenten.OrderBy(k => k.ErstelltAm).ThenBy(k => k.Id).ToListAsync(ct);
        var (treffer, erkennung) = Suchen(alle, aussteller, kontext.Extraktion!.Volltext);
        if (treffer is null)
        {
            treffer = Neu(kontext, aussteller);
            db.Korrespondenten.Add(treffer);
            erkennung = KorrespondentErkennung.Name;
        }
        else
        {
            Ergaenzen(treffer, aussteller, kontext.Vorgang.MailAbsender);
        }

        beleg.KorrespondentId = treffer.Id;
        beleg.Korrespondent = treffer;
        beleg.KorrespondentErkennung = erkennung;
        return SchrittErgebnis.Weiter;
    }

    private static (Korrespondent? Treffer, KorrespondentErkennung Erkennung) Suchen(
        List<Korrespondent> alle, ExtrahiertePartei aussteller, string? volltext)
    {
        if (KennungNormalisierer.UstIdNr(aussteller.UstIdNr) is { } ust
            && alle.FirstOrDefault(k => KennungNormalisierer.UstIdNr(k.UstIdNr) == ust) is { } nachUst)
        {
            return (nachUst, KorrespondentErkennung.UstIdNr);
        }

        var ibans = aussteller.Bankverbindungen.Select(b => KennungNormalisierer.Iban(b.Iban)).OfType<string>().ToHashSet();
        if (ibans.Count > 0
            && alle.FirstOrDefault(k => k.Bankverbindungen.Any(b => KennungNormalisierer.Iban(b.Iban) is { } n && ibans.Contains(n))) is { } nachIban)
        {
            return (nachIban, KorrespondentErkennung.Iban);
        }

        if (!string.IsNullOrWhiteSpace(aussteller.Name) && KennungNormalisierer.Name(aussteller.Name) is { Length: > 0 } name
            && alle.FirstOrDefault(k => k.Aliase.Prepend(k.Name).Any(n => KennungNormalisierer.Name(n) == name)) is { } nachName)
        {
            return (nachName, KorrespondentErkennung.Name);
        }

        var nachRegel = alle.FirstOrDefault(k =>
            k.Matching.Algorithmus is not (MatchingAlgorithmus.Keiner or MatchingAlgorithmus.Auto) && k.Matching.Trifft(volltext));
        return nachRegel is null ? (null, KorrespondentErkennung.Keine) : (nachRegel, KorrespondentErkennung.Name);
    }

    private Korrespondent Neu(VerarbeitungsKontext kontext, ExtrahiertePartei aussteller)
    {
        var name = string.IsNullOrWhiteSpace(aussteller.Name)
            ? $"Unbekannt {(string.IsNullOrWhiteSpace(kontext.Extraktion?.Rechnungsnummer) ? kontext.Dateiname : kontext.Extraktion.Rechnungsnummer)}"
            : aussteller.Name.Trim();
        var korrespondent = new Korrespondent
        {
            Id = Guid.NewGuid(),
            Name = name,
            Anschrift = new Anschrift
            {
                Strasse = Leer(aussteller.Strasse),
                Plz = Leer(aussteller.Plz),
                Ort = Leer(aussteller.Ort),
                Land = Leer(aussteller.Land),
            },
            Email = Leer(aussteller.Email),
            Website = Leer(aussteller.Website),
            IstLieferant = true,
            Geprueft = false,
            ErstelltAm = zeit.GetUtcNow(),
        };
        Ergaenzen(korrespondent, aussteller, kontext.Vorgang.MailAbsender);
        return korrespondent;
    }

    /// <summary>Füllt leere Kennungen und ergänzt Bankverbindungen; vorhandene Werte bleiben.</summary>
    private static void Ergaenzen(Korrespondent korrespondent, ExtrahiertePartei aussteller, string? mailAbsender)
    {
        if (string.IsNullOrWhiteSpace(korrespondent.UstIdNr))
        {
            korrespondent.UstIdNr = KennungNormalisierer.UstIdNr(aussteller.UstIdNr);
        }

        if (string.IsNullOrWhiteSpace(korrespondent.Steuernummer))
        {
            korrespondent.Steuernummer = Leer(aussteller.Steuernummer);
        }

        BankverbindungenErgaenzen(korrespondent, aussteller);

        if (string.IsNullOrWhiteSpace(korrespondent.Domain))
        {
            korrespondent.Domain = DomainErmitteln(aussteller, mailAbsender);
        }
    }

    /// <summary>Hängt unbekannte IBANs an und trägt einen fehlenden BIC zu einer bekannten IBAN nach.</summary>
    private static void BankverbindungenErgaenzen(Korrespondent korrespondent, ExtrahiertePartei aussteller)
    {
        var liste = korrespondent.Bankverbindungen.ToList();
        var geaendert = false;
        foreach (var erkannt in aussteller.Bankverbindungen)
        {
            if (KennungNormalisierer.Iban(erkannt.Iban) is not { } iban)
            {
                continue;
            }

            var bic = KennungNormalisierer.Bic(erkannt.Bic);
            var index = liste.FindIndex(b => KennungNormalisierer.Iban(b.Iban) == iban);
            if (index < 0)
            {
                liste.Add(new Bankverbindung(iban, bic));
                geaendert = true;
            }
            else if (bic is not null && string.IsNullOrWhiteSpace(liste[index].Bic))
            {
                liste[index] = liste[index] with { Bic = bic };
                geaendert = true;
            }
        }

        if (geaendert)
        {
            korrespondent.Bankverbindungen = liste; // neue Liste, damit EF die Änderung sieht
        }
    }

    private static string? DomainErmitteln(ExtrahiertePartei aussteller, string? mailAbsender)
    {
        if (KorrespondentNormalisierung.Domain(aussteller.Website) is { } ausWebsite)
        {
            return ausWebsite;
        }

        return MailDomain(aussteller.Email) ?? MailDomain(mailAbsender);
    }

    private static string? MailDomain(string? adresse)
    {
        if (string.IsNullOrWhiteSpace(adresse))
        {
            return null;
        }

        var at = adresse.LastIndexOf('@');
        if (at < 0)
        {
            return null;
        }

        var domain = adresse[(at + 1)..].Trim().TrimEnd('>').Trim().ToLowerInvariant();
        return domain.Length == 0 || FreemailDomains.Contains(domain) ? null : domain;
    }

    private static bool HatAngaben(ExtrahiertePartei p) =>
        !string.IsNullOrWhiteSpace(p.Name) || !string.IsNullOrWhiteSpace(p.UstIdNr) || !string.IsNullOrWhiteSpace(p.Steuernummer)
        || p.Bankverbindungen.Any(b => !string.IsNullOrWhiteSpace(b.Iban)) || !string.IsNullOrWhiteSpace(p.Email) || !string.IsNullOrWhiteSpace(p.Website)
        || !string.IsNullOrWhiteSpace(p.Strasse) || !string.IsNullOrWhiteSpace(p.Ort);

    private static string? Leer(string? wert) => string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
}
