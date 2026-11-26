using System.Xml;
using System.Xml.Linq;
using Biber.Invoice.Domain.Entitaeten;
using Biber.Invoice.Domain.Enums;
using Biber.Invoice.Domain.Pipeline;
using Biber.Invoice.Domain.Regeln;
using s2industries.ZUGFeRD;

namespace Biber.Invoice.Infrastructure.Module.ERechnung;

/// <summary>
/// Liest CII (ZUGFeRD 1/2, Factur-X, XRechnung CII) und UBL (Invoice, CreditNote) über ZUGFeRD-csharp,
/// das beide Syntaxen lädt. Das Wurzelelement wird vorher geprüft, damit fremdes XML sauber abgewiesen wird.
/// </summary>
public sealed class ERechnungParser : IERechnungParser
{
    private const string NsUblInvoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string NsUblCreditNote = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

    private static readonly XmlReaderSettings LeseEinstellungen = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    private static readonly Dictionary<string, string> Einheiten = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C62"] = "Stück",
        ["H87"] = "Stück",
        ["KGM"] = "kg",
        ["LTR"] = "l",
        ["MTR"] = "m",
        ["HUR"] = "Std.",
        ["MON"] = "Monat",
    };

    public ExtraktionsErgebnis Parsen(byte[] xml)
    {
        var profilkennung = PruefeWurzel(xml);

        InvoiceDescriptor rechnung;
        try
        {
            using var strom = new MemoryStream(xml, writable: false);
            rechnung = InvoiceDescriptor.Load(strom);
        }
        catch (Exception ex)
        {
            throw new ERechnungFormatException("Die E-Rechnung konnte nicht gelesen werden.", ex);
        }

        return Abbilden(rechnung, profilkennung);
    }

    /// <summary>Klartext zu einem UN/ECE-Einheitencode (Rec. 20); unbekannte Codes bleiben stehen.</summary>
    public static string? EinheitKlartext(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : Einheiten.GetValueOrDefault(code.Trim(), code.Trim());

    /// <summary>
    /// <c>ZUGFeRD</c>, wenn die Profilkennung ZUGFeRD (1: <c>urn:ferd:…</c>) oder Factur-X nennt, sonst <c>XRechnung</c>.
    /// Das reine EN-16931-Profil nennt keines von beiden; bei eingebettetem XML setzt der Schritt die Quelle.
    /// </summary>
    public static ExtraktionsQuelle QuelleAusProfil(string? profilkennung) =>
        profilkennung is not null
        && (profilkennung.Contains("zugferd", StringComparison.OrdinalIgnoreCase)
            || profilkennung.Contains("factur-x", StringComparison.OrdinalIgnoreCase)
            || profilkennung.StartsWith("urn:ferd:", StringComparison.OrdinalIgnoreCase))
            ? ExtraktionsQuelle.ZUGFeRD
            : ExtraktionsQuelle.XRechnung;

    /// <summary>Prüft, ob das XML wohlgeformt und CII oder UBL ist, und liefert die Profilkennung.</summary>
    private static string? PruefeWurzel(byte[] xml)
    {
        XDocument dokument;
        try
        {
            using var strom = new MemoryStream(xml, writable: false);
            using var leser = XmlReader.Create(strom, LeseEinstellungen);
            dokument = XDocument.Load(leser);
        }
        catch (XmlException ex)
        {
            throw new ERechnungFormatException("Das XML der E-Rechnung ist nicht wohlgeformt.", ex);
        }

        var wurzel = dokument.Root!;
        var name = wurzel.Name.LocalName;
        var ns = wurzel.Name.NamespaceName;

        if (name is "CrossIndustryInvoice" or "CrossIndustryDocument")
        {
            return wurzel.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "GuidelineSpecifiedDocumentContextParameter")?
                .Elements().FirstOrDefault(e => e.Name.LocalName == "ID")?.Value.Trim();
        }

        if ((name == "Invoice" && ns == NsUblInvoice) || (name == "CreditNote" && ns == NsUblCreditNote))
        {
            return wurzel.Elements().FirstOrDefault(e => e.Name.LocalName == "CustomizationID")?.Value.Trim();
        }

        throw new ERechnungFormatException($"Unbekanntes Wurzelelement „{name}“; erwartet CII oder UBL.");
    }

    private static ExtraktionsErgebnis Abbilden(InvoiceDescriptor r, string? profilkennung)
    {
        var aussteller = Partei(
            r.Seller, r.SellerContact, r.SellerElectronicAddress, r.SellerTaxRegistration, r.CreditorBankAccounts);
        var empfaenger = Partei(
            r.Buyer, r.BuyerContact, r.BuyerElectronicAddress, r.BuyerTaxRegistration, r.DebitorBankAccounts);

        var positionen = new List<ExtrahiertePosition>();
        foreach (var (p, i) in r.TradeLineItems.Select((p, i) => (p, i)))
        {
            // Manche Warenwirtschaften schreiben Folgezeilen des Positionstexts (z. B. „incl. Montage“) als eigene
            // Position mit Menge 0 und Betrag 0. Die gehört als Zusatztext zur vorherigen Position.
            if (positionen.Count > 0 && IstNull(p.BilledQuantity) && IstNull(p.LineTotalAmount))
            {
                var zusatz = Leer(p.Description) ?? Leer(p.Name);
                if (zusatz is not null)
                {
                    var vorige = positionen[^1];
                    positionen[^1] = vorige with { Text = vorige.Text.Length == 0 ? zusatz : $"{vorige.Text} – {zusatz}" };
                }

                continue;
            }

            positionen.Add(new ExtrahiertePosition(
                Nr: int.TryParse(p.AssociatedDocument?.LineID, out var nr) ? nr : i + 1,
                Text: PositionsText(p.Name, p.Description),
                Menge: p.BilledQuantity,
                // Codes mit führender Ziffer heißen in der Bibliothek z. B. „_3B“.
                Einheit: EinheitKlartext(p.UnitCode?.ToString().TrimStart('_')),
                Einzelpreis: p.NetUnitPrice,
                Gesamt: p.LineTotalAmount,
                Steuersatz: p.TaxPercent,
                Artikelnummer: ArtikelnummerNormalisierer.Bereinigen(
                    Leer(p.SellerAssignedID) ?? Leer(p.GlobalID?.ID) ?? Leer(p.BuyerAssignedID)),
                Variante: Variante(p.ApplicableProductCharacteristics)));
        }

        var steuerzeilen = r.Taxes
            .Select(t => new ExtrahierteSteuerzeile(t.Percent, t.BasisAmount, t.TaxAmount))
            .ToList();

        var volltext = new[] { r.InvoiceNo }
            .Concat(ParteiTexte(aussteller))
            .Concat(ParteiTexte(empfaenger))
            .Concat(positionen.Select(p => p.Text))
            .Concat(r.PaymentTerms.Select(z => z.Description))
            .Concat(r.Notes.Select(n => n.Content))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim());

        return new ExtraktionsErgebnis
        {
            Quelle = QuelleAusProfil(profilkennung),
            Belegart = r.Type == InvoiceType.CreditNote ? Belegart.Gutschrift : Belegart.Rechnung,
            Rechnungsnummer = Leer(r.InvoiceNo),
            Rechnungsdatum = Datum(r.InvoiceDate),
            Leistungsdatum = Datum(r.ActualDeliveryDate),
            LeistungVon = Datum(r.BillingPeriodStart),
            LeistungBis = Datum(r.BillingPeriodEnd),
            Faelligkeitsdatum = Datum(r.PaymentTerms.FirstOrDefault(z => z.DueDate.HasValue)?.DueDate),
            Waehrung = r.Currency.ToString(),
            Netto = r.TaxBasisAmount,
            Steuer = r.TaxTotalAmount,
            Brutto = r.GrandTotalAmount,
            Steuerzeilen = steuerzeilen,
            Positionen = positionen,
            Aussteller = aussteller,
            Empfaenger = empfaenger,
            Volltext = string.Join('\n', volltext),
            Skonto = Skonto(r),
            BezugRechnungsnummer = Leer(r.GetInvoiceReferencedDocuments().FirstOrDefault()?.ID),
            Konfidenz = null,
            Seiten = 1,
        };
    }

    private static ExtrahiertePartei? Partei(
        Party? partei,
        Contact? kontakt,
        ElectronicAddress? elektronisch,
        IEnumerable<TaxRegistration>? steuer,
        IEnumerable<BankAccount>? konten)
    {
        if (partei is null)
        {
            return null;
        }

        var registrierungen = steuer?.ToList() ?? [];
        var email = Leer(kontakt?.EmailAddress)
            ?? (elektronisch?.ElectronicAddressSchemeID == ElectronicAddressSchemeIdentifiers.ElectronicMailSmtp
                ? Leer(elektronisch.Address)
                : null);

        return new ExtrahiertePartei(
            Name: Leer(partei.Name),
            Strasse: Leer(partei.Street),
            Plz: Leer(partei.Postcode),
            Ort: Leer(partei.City),
            Land: partei.Country?.ToString(),
            UstIdNr: Leer(registrierungen.FirstOrDefault(t => t.SchemeID == TaxRegistrationSchemeID.VA)?.No),
            Steuernummer: Leer(registrierungen.FirstOrDefault(t => t.SchemeID == TaxRegistrationSchemeID.FC)?.No),
            Bankverbindungen: (konten ?? [])
                .Select(k => (Iban: Leer(k.IBAN), Bic: Leer(k.BIC)))
                .Where(k => k.Iban is not null)
                .DistinctBy(k => k.Iban)
                .Select(k => new Bankverbindung(k.Iban!, k.Bic))
                .ToList(),
            Email: email,
            Website: null);
    }

    /// <summary>
    /// Skonto aus den strukturierten Zahlungsbedingungen (CII <c>ApplicableTradePaymentDiscountTerms</c>). Die
    /// XRechnung-Syntax <c>#SKONTO#…#</c> im Beschreibungstext liest später der <see cref="SkontoErkenner"/>.
    /// </summary>
    private static SkontoAngabe? Skonto(InvoiceDescriptor r)
    {
        var bedingung = r.PaymentTerms.FirstOrDefault(z => z.PaymentTermsType == PaymentTermsType.Skonto && z.Percentage > 0);
        if (bedingung?.Percentage is not { } prozent)
        {
            return null;
        }

        var angabe = new SkontoAngabe(prozent, bedingung.DueDays, Datum(bedingung.MaturityDate));
        return SkontoErkenner.Plausibel(angabe.Prozent, angabe.Tage) && (angabe.Tage is not null || angabe.Bis is not null)
            ? angabe
            : null;
    }

    private static IEnumerable<string?> ParteiTexte(ExtrahiertePartei? p) =>
        p is null ? [] : [p.Name, p.Strasse, p.Plz, p.Ort, p.UstIdNr, p.Steuernummer, p.Email];

    private static string PositionsText(string? name, string? beschreibung)
    {
        var n = Leer(name);
        var b = Leer(beschreibung);
        return n is null ? b ?? "" : b is null || b == n ? n : $"{n} – {b}";
    }

    /// <summary>Artikelmerkmale (BT-160/BT-161) wie „Größe: XL, Farbe: schwarz“.</summary>
    private static string? Variante(IEnumerable<ApplicableProductCharacteristic>? merkmale) =>
        ArtikelnummerNormalisierer.Variante(string.Join(", ", (merkmale ?? [])
            .Select(m => (Name: Leer(m.Description), Wert: Leer(m.Value)))
            .Where(m => m.Wert is not null)
            .Select(m => m.Name is null ? m.Wert : $"{m.Name}: {m.Wert}")));

    private static bool IstNull(decimal? wert) => wert is null or 0m;

    private static string? Leer(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static DateOnly? Datum(DateTime? datum) => datum is { } d ? DateOnly.FromDateTime(d) : null;
}
