# Testdateien E-Rechnung

Alle Dateien sind frei erfunden und mit **ZUGFeRD-csharp 18.0.1** (`InvoiceDescriptor.Save`, Version 2.3)
erzeugt – keine echten Firmen, Steuernummern oder Konten (die IBAN ist die übliche Beispiel-IBAN).
Verkäufer ist jeweils „Muster Bürobedarf GmbH“ (Berlin), Käufer „Biber Handwerk e.K.“ (München).

| Datei | Syntax / Profil | Inhalt |
|---|---|---|
| `xrechnung-cii.xml` | CII, XRechnung 3.0 | 2 Positionen (H87, HUR), 19 %, Leistungsdatum, Fälligkeit |
| `xrechnung-ubl-invoice.xml` | UBL Invoice, XRechnung 3.0 | 2 Positionen (C62, LTR), 19 % |
| `ubl-creditnote.xml` | UBL CreditNote, XRechnung 3.0 | Gutschrift zu RE-2026-0002, ohne Fälligkeitsdatum |
| `zugferd-en16931.xml` | CII, ZUGFeRD 2 Profil EN 16931 (Comfort) | 2 Positionen (MTR, KGM mit Bruchmenge) |
| `zwei-steuersaetze.xml` | CII, XRechnung 3.0 | 19 % und 7 % |
| `leistungszeitraum.xml` | CII, XRechnung 3.0 | Leistungszeitraum 01.06.–30.06.2026 (MON) |
| `factur-x-basic.xml` | CII, Factur-X Basic | Profilkennung nennt `factur-x`, Einheit DAY ohne Klartext, E-Mail nur als elektronische Adresse |

Neu erzeugen: kleines Konsolenprogramm mit Paketverweis `ZUGFeRD-csharp` 18.0.1, das die Werte aus der
Tabelle bzw. den Erwartungswerten in `ERechnungParserTests` setzt und `Save(…, ZUGFeRDVersion.Version23, profil, format)` aufruft.
