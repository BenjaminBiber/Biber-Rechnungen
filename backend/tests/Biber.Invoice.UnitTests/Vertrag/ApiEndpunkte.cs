using Biber.Invoice.Contracts.Dto;
using Biber.Invoice.Contracts.Dto.Artikel;
using Biber.Invoice.Contracts.Dto.Auswertung;
using Biber.Invoice.Contracts.Dto.Auth;
using Biber.Invoice.Contracts.Dto.Belege;
using Biber.Invoice.Contracts.Dto.Benutzer;
using Biber.Invoice.Contracts.Dto.Einstellungen;
using Biber.Invoice.Contracts.Dto.Export;
using Biber.Invoice.Contracts.Dto.Import;
using Biber.Invoice.Contracts.Dto.Konto;
using Biber.Invoice.Contracts.Dto.Korrespondenten;
using Biber.Invoice.Contracts.Dto.Sonstiges;
using Biber.Invoice.Contracts.Dto.Tags;
using Biber.Invoice.Domain.Enums;
using static Biber.Invoice.Contracts.ApiRouten;
using R = Biber.Invoice.Contracts.Richtlinien;

namespace Biber.Invoice.UnitTests.Vertrag;

/// <summary>Eine Operation der HTTP-Schnittstelle. <see cref="Richtlinie"/> <c>null</c> = anonym.</summary>
internal sealed record Endpunkt(string Methode, string Pfad, string? Richtlinie, string Zusammenfassung)
{
    public Type? Body { get; init; }

    /// <summary>Body ist genau einer dieser Typen (je nach Pfadparameter).</summary>
    public Type[] BodyVarianten { get; init; } = [];

    /// <summary>Feldname bei <c>multipart/form-data</c>.</summary>
    public string? Multipart { get; init; }

    public bool MultipartMehrere { get; init; }

    public Type? Antwort { get; init; }

    /// <summary>Medientyp, wenn die Antwort eine Datei ist.</summary>
    public string? Datei { get; init; }

    public int Status { get; init; } = 200;

    public Abfrage[] Query { get; init; } = [];

    /// <summary>Record, dessen Eigenschaften einzeln als Query-Parameter kommen.</summary>
    public Type? QueryObjekt { get; init; }
}

internal sealed record Abfrage(string Name, Type Typ, object? Standard = null, string[]? Werte = null, bool Pflicht = false);

/// <summary>Die vollständige HTTP-Schnittstelle; Quelle für <c>docs/api/openapi.json</c>.</summary>
internal static class ApiEndpunkte
{
    private static Endpunkt Get(string pfad, string? richtlinie, string text) => new("get", pfad, richtlinie, text);

    private static Endpunkt Post(string pfad, string? richtlinie, string text) => new("post", pfad, richtlinie, text);

    private static Endpunkt Put(string pfad, string? richtlinie, string text) => new("put", pfad, richtlinie, text);

    private static Endpunkt Delete(string pfad, string? richtlinie, string text) => new("delete", pfad, richtlinie, text) { Status = 204 };

    private static readonly Abfrage Jahr = new("jahr", typeof(int?));
    private static readonly Abfrage Seite = new("seite", typeof(int), 1);

    public static readonly IReadOnlyList<Endpunkt> Alle =
    [
        // Auth
        Get(Auth.Status, null, "Einrichtungs- und Anmeldestatus") with { Antwort = typeof(AuthStatusDto) },
        Get(Auth.TotpVorschlag, null, "TOTP-Schlüssel für die Ersteinrichtung (nur solange nicht eingerichtet)") with { Antwort = typeof(TotpEinrichtungDto) },
        Post(Auth.Ersteinrichtung, null, "Ersten Benutzer als Admin anlegen") with { Body = typeof(ErsteinrichtungDto), Antwort = typeof(BenutzerDto), Status = 201 },
        Post(Auth.Anmelden, null, "Mit E-Mail und Passwort anmelden") with { Body = typeof(AnmeldenDto), Antwort = typeof(AnmeldeErgebnisDto) },
        Post(Auth.Totp, null, "Zweiten Faktor (TOTP) bestätigen") with { Body = typeof(TotpDto), Antwort = typeof(AnmeldeErgebnisDto) },
        Post(Auth.Wiederherstellungscode, null, "Mit Wiederherstellungscode anmelden") with { Body = typeof(CodeDto), Antwort = typeof(AnmeldeErgebnisDto) },
        Post(Auth.Abmelden, R.Lesen, "Abmelden") with { Status = 204 },
        Post(Auth.Registrieren, null, "Konto registrieren; wartet auf Freigabe") with { Body = typeof(RegistrierenDto), Status = 202 },
        Get(Auth.GoogleStart, null, "Google-Anmeldung starten (Weiterleitung)") with { Status = 302, Query = [new("verknuepfen", typeof(bool), false)] },
        Get(Auth.GoogleRueckruf, null, "Rückruf der Google-Anmeldung (Weiterleitung)") with { Status = 302 },

        // Konto
        Get(Konto.Eigenes, R.Lesen, "Eigenes Konto") with { Antwort = typeof(KontoDto) },
        Put(Konto.Eigenes, R.Lesen, "Name und E-Mail ändern") with { Body = typeof(KontoAendernDto), Antwort = typeof(KontoDto) },
        Post(Konto.Passwort, R.Lesen, "Passwort ändern") with { Body = typeof(PasswortAendernDto), Status = 204 },
        Post(Konto.TotpEinrichten, R.Lesen, "TOTP-Einrichtung beginnen") with { Antwort = typeof(TotpEinrichtungDto) },
        Post(Konto.TotpAktivieren, R.Lesen, "TOTP mit Code aktivieren") with { Body = typeof(CodeDto), Antwort = typeof(WiederherstellungscodesDto) },
        Post(Konto.TotpDeaktivieren, R.Lesen, "TOTP deaktivieren") with { Status = 204 },
        Post(Konto.Wiederherstellungscodes, R.Lesen, "Neue Wiederherstellungscodes erzeugen") with { Antwort = typeof(WiederherstellungscodesDto) },
        Post(Konto.AndereSitzungenAbmelden, R.Lesen, "Alle anderen Sitzungen abmelden") with { Status = 204 },

        // Benutzer
        Get(Benutzer.Liste, R.Admin, "Alle Benutzer") with { Antwort = typeof(BenutzerDto[]) },
        Post(Benutzer.Liste, R.Admin, "Benutzer anlegen") with { Body = typeof(BenutzerAnlegenDto), Antwort = typeof(BenutzerDto), Status = 201 },
        Put(Benutzer.Einzeln, R.Admin, "Rolle und Aktiv-Status ändern") with { Body = typeof(BenutzerAendernDto), Antwort = typeof(BenutzerDto) },
        Post(Benutzer.Passwort, R.Admin, "Passwort eines Benutzers setzen") with { Body = typeof(NeuesPasswortDto), Status = 204 },
        Post(Benutzer.Freigeben, R.Admin, "Registriertes Konto freigeben") with { Body = typeof(BenutzerFreigebenDto), Antwort = typeof(BenutzerDto) },
        Post(Benutzer.Ablehnen, R.Admin, "Registriertes Konto ablehnen") with { Status = 204 },

        // Belege lesen
        Get(Belege.Liste, R.Lesen, "Belege suchen und filtern") with { QueryObjekt = typeof(BelegFilterDto), Antwort = typeof(BelegListeDto) },
        Get(Belege.Zaehler, R.Lesen, "Zähler der Schnellansichten") with { Antwort = typeof(BelegZaehlerDto) },
        Get(Belege.Einzeln, R.Lesen, "Beleg-Detail") with { Antwort = typeof(BelegDetailDto) },
        Get(Belege.Nachbarn, R.Lesen, "Vorheriger und nächster Beleg in der gefilterten Liste") with { QueryObjekt = typeof(BelegFilterDto), Antwort = typeof(BelegNachbarnDto) },
        Get(Belege.Aehnliche, R.Lesen, "Ähnliche Belege") with { Query = [new("anzahl", typeof(int), 5)], Antwort = typeof(BelegListeneintragDto[]) },
        Get(Belege.Historie, R.Lesen, "Änderungsprotokoll des Belegs") with { Antwort = typeof(AuditEintragDto[]) },
        Get(Belege.Datei, R.Lesen, "Datei des Belegs herunterladen oder anzeigen") with { Query = [new("inline", typeof(bool), false)], Datei = "application/octet-stream" },
        Get(Belege.Vorschau, R.Lesen, "Vorschaubild einer Seite") with { Query = [new("seite", typeof(int), 1)], Datei = "image/png" },
        Get(Belege.Visualisierung, R.Lesen, "XRechnung-Visualisierung") with { Datei = "text/html" },
        Get(Ansichten.Liste, R.Lesen, "Gespeicherte Ansichten") with { Antwort = typeof(GespeicherteAnsichtDto[]) },
        Post(Ansichten.Liste, R.Lesen, "Ansicht speichern") with { Body = typeof(GespeicherteAnsichtAnlegenDto), Antwort = typeof(GespeicherteAnsichtDto), Status = 201 },
        Delete(Ansichten.Einzeln, R.Lesen, "Ansicht löschen"),

        // Belege schreiben
        Put(Belege.Einzeln, R.Bearbeiten, "Kopfdaten ändern") with { Body = typeof(BelegAendernDto), Antwort = typeof(BelegDetailDto) },
        Put(Belege.Positionen, R.Bearbeiten, "Positionen ersetzen") with { Body = typeof(PositionAendernDto[]), Antwort = typeof(BelegDetailDto) },
        Post(Belege.Tags, R.Bearbeiten, "Tag setzen") with { Body = typeof(TagZuordnenDto), Status = 204 },
        Delete(Belege.Tag, R.Bearbeiten, "Tag entfernen"),
        Post(Belege.Bestaetigen, R.Bearbeiten, "Beleg bestätigen (entfernt „zu prüfen“)") with { Body = typeof(BestaetigenDto), Status = 204 },
        Post(Belege.ErneutAuslesen, R.Bearbeiten, "Extraktion erneut anstoßen") with { Status = 202 },
        Post(Belege.Dublette, R.Bearbeiten, "Dubletten-Markierung setzen oder aufheben") with { Body = typeof(DubletteSetzenDto), Status = 204 },
        Post(Belege.Massenaktion, R.Bearbeiten, "Aktion auf mehrere Belege anwenden") with { Body = typeof(MassenaktionDto), Antwort = typeof(AnzahlGeaendertDto) },
        Post(Belege.Aufteilen, R.Bearbeiten, "Beleg nach Seiten aufteilen") with { Body = typeof(AufteilenDto), Antwort = typeof(ImportVorgangDto[]), Status = 202 },
        Post(Belege.LogoUebernehmen, R.Bearbeiten, "Ausschnitt als Logo des Korrespondenten übernehmen") with { Body = typeof(LogoAusschnittDto), Antwort = typeof(LogoDto) },

        // Import
        Post(Import.Upload, R.Bearbeiten, "Dateien hochladen") with { Multipart = "dateien", MultipartMehrere = true, Antwort = typeof(ImportVorgangDto[]), Status = 202 },
        Get(Import.Vorgaenge, R.Lesen, "Import-Vorgänge") with { Query = [new("quelle", typeof(ImportQuelle?)), new("tage", typeof(int), 7), Seite], Antwort = typeof(SeiteDto<ImportVorgangDto>) },
        Get(Import.Vorgang, R.Lesen, "Ein Import-Vorgang") with { Antwort = typeof(ImportVorgangDto) },

        // Korrespondenten
        Get(Korrespondenten.Liste, R.Lesen, "Korrespondenten") with
        {
            Query = [new("suche", typeof(string)), new("typ", typeof(string), "Alle", ["Lieferanten", "Kunden", "Alle"]), Jahr],
            Antwort = typeof(KorrespondentListeneintragDto[]),
        },
        Post(Korrespondenten.Liste, R.Bearbeiten, "Korrespondent anlegen") with { Body = typeof(KorrespondentAendernDto), Antwort = typeof(KorrespondentDetailDto), Status = 201 },
        Get(Korrespondenten.Einzeln, R.Lesen, "Korrespondent-Detail") with { Query = [Jahr], Antwort = typeof(KorrespondentDetailDto) },
        Put(Korrespondenten.Einzeln, R.Bearbeiten, "Korrespondent ändern") with { Body = typeof(KorrespondentAendernDto), Antwort = typeof(KorrespondentDetailDto) },
        Post(Korrespondenten.Zusammenfuehren, R.Bearbeiten, "Korrespondenten zusammenführen") with { Body = typeof(KorrespondentenZusammenfuehrenDto), Antwort = typeof(KorrespondentDetailDto) },
        Get(Korrespondenten.Logo, R.Lesen, "Logo-Bild") with { Datei = "image/png" },
        Put(Korrespondenten.Logo, R.Bearbeiten, "Logo hochladen") with { Multipart = "datei", Antwort = typeof(LogoDto) },
        Delete(Korrespondenten.Logo, R.Bearbeiten, "Logo entfernen"),
        Post(Korrespondenten.LogoFavicon, R.Bearbeiten, "Favicon der Website als Logo laden") with { Antwort = typeof(LogoDto) },

        // Tags, Kategorien, Matching
        Get(Tags.Liste, R.Lesen, "Tags") with { Antwort = typeof(TagDto[]) },
        Post(Tags.Liste, R.Bearbeiten, "Tag anlegen") with { Body = typeof(TagAendernDto), Antwort = typeof(TagDto), Status = 201 },
        Put(Tags.Einzeln, R.Bearbeiten, "Tag ändern") with { Body = typeof(TagAendernDto), Antwort = typeof(TagDto) },
        Delete(Tags.Einzeln, R.Bearbeiten, "Tag löschen"),
        Get(Kategorien.Liste, R.Lesen, "Kategorien") with { Query = [Jahr], Antwort = typeof(KategorieDto[]) },
        Post(Kategorien.Liste, R.Bearbeiten, "Kategorie anlegen") with { Body = typeof(KategorieAendernDto), Antwort = typeof(KategorieDto), Status = 201 },
        Put(Kategorien.Einzeln, R.Bearbeiten, "Kategorie ändern") with { Body = typeof(KategorieAendernDto), Antwort = typeof(KategorieDto) },
        Delete(Kategorien.Einzeln, R.Bearbeiten, "Kategorie löschen"),
        Post(Matching.Testen, R.Bearbeiten, "Matching-Regel gegen den Bestand testen") with { Body = typeof(MatchingTestDto), Antwort = typeof(MatchingTestErgebnisDto) },
        Post(Matching.Anwenden, R.Bearbeiten, "Matching-Regel auf alle Belege anwenden") with { Body = typeof(MatchingAnwendenDto), Antwort = typeof(AnzahlGeaendertDto) },
        Get(Matching.AutoStatus, R.Lesen, "Trainingsstand des Auto-Matchings") with { Antwort = typeof(AutoMatchingStatusDto[]) },

        // Artikel
        Get(Artikel.Liste, R.Lesen, "Artikel suchen") with { QueryObjekt = typeof(ArtikelFilterDto), Antwort = typeof(SeiteDto<ArtikelListeneintragDto>) },
        Get(Artikel.Einzeln, R.Lesen, "Preisdetail eines Artikels") with { Query = [Jahr], Antwort = typeof(PreisDetailDto) },
        Get(Stammartikel.Liste, R.Lesen, "Stammartikel") with { Antwort = typeof(StammartikelListeneintragDto[]) },
        Post(Stammartikel.Liste, R.Bearbeiten, "Stammartikel anlegen") with { Body = typeof(StammartikelAendernDto), Antwort = typeof(StammartikelListeneintragDto), Status = 201 },
        Get(Stammartikel.Einzeln, R.Lesen, "Preisdetail eines Stammartikels") with { Query = [Jahr], Antwort = typeof(PreisDetailDto) },
        Put(Stammartikel.Einzeln, R.Bearbeiten, "Stammartikel ändern") with { Body = typeof(StammartikelAendernDto), Antwort = typeof(StammartikelListeneintragDto) },
        Delete(Stammartikel.Artikel, R.Bearbeiten, "Artikel aus dem Stammartikel lösen"),

        // Auswertung und System
        Get(Auswertung.Dashboard, R.Lesen, "Dashboard") with
        {
            Query = [new("zeitraum", typeof(string), "Monat", ["Monat", "Quartal", "Jahr"]), Jahr],
            Antwort = typeof(DashboardDto),
        },
        Get(SystemInfo.Status, R.Lesen, "Systemstatus aller Module") with { Antwort = typeof(SystemStatusEintragDto[]) },
        Get(SystemInfo.Backup, R.Lesen, "Stand der Datenbank-Sicherung") with { Antwort = typeof(BackupStatusDto) },

        // Export
        Post(Export.Vorschau, R.Bearbeiten, "Vorschau eines Exports") with { Body = typeof(ExportAnfrageDto), Antwort = typeof(ExportVorschauDto) },
        Post(Export.Erzeugen, R.Bearbeiten, "Export erzeugen") with { Body = typeof(ExportAnfrageDto), Antwort = typeof(ExportLaufDto), Status = 201 },
        Get(Export.Laeufe, R.Lesen, "Bisherige Exporte") with { Antwort = typeof(ExportLaufDto[]) },
        Get(Export.LaufDatei, R.Lesen, "Export herunterladen") with { Datei = "application/octet-stream" },

        // Einstellungen
        Get(Einstellungen.Alle, R.Lesen, "Alle Einstellungen") with { Antwort = typeof(EinstellungenDto) },
        Put(Einstellungen.Gruppe, R.Admin, "Eine Einstellungsgruppe speichern") with
        {
            BodyVarianten =
            [
                typeof(AllgemeinEinstellungen), typeof(UstEinstellungen), typeof(PruefEinstellungen), typeof(DiEinstellungen),
                typeof(MailEinstellungen), typeof(OrdnerEinstellungen), typeof(AufbewahrungEinstellungen), typeof(ZugriffEinstellungen),
            ],
            Status = 204,
        },
        Get(Einstellungen.DiVerbrauch, R.Lesen, "Document-Intelligence-Verbrauch des Monats") with { Antwort = typeof(DiVerbrauchDto) },
        Post(Einstellungen.DiVerbindungTesten, R.Admin, "Verbindung zu Document Intelligence testen") with { Antwort = typeof(AktionErgebnisDto) },
        Post(Einstellungen.DiFortsetzen, R.Admin, "Pausierte Verarbeitung fortsetzen") with { Antwort = typeof(AktionErgebnisDto) },
        Post(Einstellungen.MailVerbindungTesten, R.Admin, "IMAP-Verbindung testen") with { Antwort = typeof(AktionErgebnisDto) },
        Post(Einstellungen.MailJetztAbrufen, R.Admin, "Postfach jetzt abrufen") with { Antwort = typeof(AktionErgebnisDto) },
        Get(Einstellungen.MailStatus, R.Lesen, "Status des Mail-Abrufs") with { Antwort = typeof(MailStatusDto) },

        // Sonstiges
        Get(Benachrichtigungen.Liste, R.Lesen, "Benachrichtigungen") with { Query = [new("nurUngelesen", typeof(bool), false), Seite], Antwort = typeof(SeiteDto<BenachrichtigungDto>) },
        Get(Benachrichtigungen.Zaehler, R.Lesen, "Anzahl ungelesener Benachrichtigungen") with { Antwort = typeof(BenachrichtigungZaehlerDto) },
        Post(Benachrichtigungen.Gelesen, R.Lesen, "Benachrichtigung als gelesen markieren") with { Status = 204 },
        Post(Benachrichtigungen.AlleGelesen, R.Lesen, "Alle Benachrichtigungen als gelesen markieren") with { Status = 204 },
        Get(Aufbewahrung.Loeschbar, R.Admin, "Jahre mit abgelaufener Aufbewahrungsfrist") with { Antwort = typeof(LoeschbarDto[]) },
        Post(Aufbewahrung.Loeschen, R.Admin, "Belege eines Jahres löschen") with { Body = typeof(LoeschenDto), Antwort = typeof(LoeschErgebnisDto) },
        Get(Integritaet.Laeufe, R.Lesen, "Läufe der Integritätsprüfung") with { Antwort = typeof(IntegritaetsLaufDto[]) },
        Post(Integritaet.Starten, R.Admin, "Integritätsprüfung starten") with { Status = 202 },
        Get(Suche.Global, R.Lesen, "Globale Suche (Befehlspalette)") with { Query = [new("q", typeof(string), Pflicht: true)], Antwort = typeof(SuchergebnisDto) },
        Get(Tokens.Liste, R.Lesen, "Eigene API-Tokens") with { Antwort = typeof(ApiTokenDto[]) },
        Post(Tokens.Liste, R.Bearbeiten, "API-Token erzeugen") with { Body = typeof(ApiTokenAnlegenDto), Antwort = typeof(ApiTokenErstelltDto), Status = 201 },
        Post(Tokens.Widerrufen, R.Bearbeiten, "API-Token widerrufen") with { Status = 204 },
        Get(Verfahrensdoku.Info, R.Lesen, "Stand der Verfahrensdokumentation") with { Antwort = typeof(VerfahrensdokuDto) },
        Get(Verfahrensdoku.Pdf, R.Lesen, "Verfahrensdokumentation als PDF") with { Datei = "application/pdf" },
    ];
}
