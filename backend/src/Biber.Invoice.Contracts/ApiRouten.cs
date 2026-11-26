namespace Biber.Invoice.Contracts;

/// <summary>
/// Pfade der HTTP-Schnittstelle, vollständig ab <c>/api</c>. Methoden, Richtlinien und Schemas stehen in
/// <c>docs/api/openapi.json</c>; ein Pfad kann mehrere Methoden tragen (z. B. GET und PUT).
/// </summary>
public static class ApiRouten
{
    public static class Auth
    {
        public const string Status = "/api/auth/status";
        public const string TotpVorschlag = "/api/auth/totp-vorschlag";
        public const string Ersteinrichtung = "/api/auth/ersteinrichtung";
        public const string Anmelden = "/api/auth/anmelden";
        public const string Totp = "/api/auth/totp";
        public const string Wiederherstellungscode = "/api/auth/wiederherstellungscode";
        public const string Abmelden = "/api/auth/abmelden";
        public const string Registrieren = "/api/auth/registrieren";
        public const string GoogleStart = "/api/auth/google/start";
        public const string GoogleRueckruf = "/api/auth/google/rueckruf";
    }

    public static class Konto
    {
        /// <summary>GET, PUT</summary>
        public const string Eigenes = "/api/konto";
        public const string Passwort = "/api/konto/passwort";
        public const string TotpEinrichten = "/api/konto/totp/einrichten";
        public const string TotpAktivieren = "/api/konto/totp/aktivieren";
        public const string TotpDeaktivieren = "/api/konto/totp/deaktivieren";
        public const string Wiederherstellungscodes = "/api/konto/wiederherstellungscodes";
        public const string AndereSitzungenAbmelden = "/api/konto/sitzungen/andere-abmelden";
    }

    public static class Benutzer
    {
        /// <summary>GET, POST</summary>
        public const string Liste = "/api/benutzer";
        /// <summary>PUT</summary>
        public const string Einzeln = "/api/benutzer/{id}";
        public const string Passwort = "/api/benutzer/{id}/passwort";
        public const string Freigeben = "/api/benutzer/{id}/freigeben";
        public const string Ablehnen = "/api/benutzer/{id}/ablehnen";
    }

    public static class Belege
    {
        public const string Liste = "/api/belege";
        public const string Zaehler = "/api/belege/zaehler";
        /// <summary>GET, PUT</summary>
        public const string Einzeln = "/api/belege/{id}";
        public const string Nachbarn = "/api/belege/{id}/nachbarn";
        public const string Aehnliche = "/api/belege/{id}/aehnliche";
        public const string Historie = "/api/belege/{id}/historie";
        public const string Datei = "/api/belege/{id}/dateien/{dateiId}";
        public const string Vorschau = "/api/belege/{id}/vorschau";
        public const string Visualisierung = "/api/belege/{id}/visualisierung";
        public const string Positionen = "/api/belege/{id}/positionen";
        public const string Tags = "/api/belege/{id}/tags";
        public const string Tag = "/api/belege/{id}/tags/{tagId}";
        public const string Bestaetigen = "/api/belege/{id}/bestaetigen";
        public const string ErneutAuslesen = "/api/belege/{id}/erneut-auslesen";
        public const string Dublette = "/api/belege/{id}/dublette";
        /// <summary>POST: Gutschrift mit einer Rechnung verknüpfen oder die Verknüpfung lösen</summary>
        public const string Bezug = "/api/belege/{id}/bezug";
        /// <summary>PUT: Skonto setzen oder entfernen</summary>
        public const string Skonto = "/api/belege/{id}/skonto";
        public const string Massenaktion = "/api/belege/massenaktion";
        public const string Aufteilen = "/api/belege/{id}/aufteilen";
        public const string LogoUebernehmen = "/api/belege/{id}/logo-uebernehmen";
        /// <summary>POST (Admin): trotz Löschsperre in die gelöschten Belege verschieben</summary>
        public const string Loeschen = "/api/belege/{id}/loeschen";
        /// <summary>GET (Admin): gelöschte (falsch zugeordnete) Belege</summary>
        public const string Geloeschte = "/api/belege/geloeschte";
        /// <summary>POST (Admin): gelöschten Beleg wiederherstellen</summary>
        public const string Wiederherstellen = "/api/belege/{id}/wiederherstellen";
    }

    public static class Ansichten
    {
        /// <summary>GET, POST</summary>
        public const string Liste = "/api/ansichten";
        /// <summary>DELETE</summary>
        public const string Einzeln = "/api/ansichten/{id}";
    }

    public static class Import
    {
        public const string Upload = "/api/import/upload";
        public const string Vorgaenge = "/api/import/vorgaenge";
        public const string Vorgang = "/api/import/vorgaenge/{id}";
    }

    public static class Korrespondenten
    {
        /// <summary>GET, POST</summary>
        public const string Liste = "/api/korrespondenten";
        /// <summary>GET, PUT</summary>
        public const string Einzeln = "/api/korrespondenten/{id}";
        public const string Zusammenfuehren = "/api/korrespondenten/zusammenfuehren";
        /// <summary>GET, PUT (multipart), DELETE</summary>
        public const string Logo = "/api/korrespondenten/{id}/logo";
        public const string LogoFavicon = "/api/korrespondenten/{id}/logo/favicon";
        /// <summary>POST: USt-IdNr. sofort über VIES prüfen</summary>
        public const string UstIdPruefen = "/api/korrespondenten/{id}/ustid-pruefen";
    }

    public static class Tags
    {
        /// <summary>GET, POST</summary>
        public const string Liste = "/api/tags";
        /// <summary>PUT, DELETE</summary>
        public const string Einzeln = "/api/tags/{id}";
    }

    public static class Kategorien
    {
        /// <summary>GET, POST</summary>
        public const string Liste = "/api/kategorien";
        /// <summary>PUT, DELETE</summary>
        public const string Einzeln = "/api/kategorien/{id}";
    }

    public static class Matching
    {
        public const string Testen = "/api/matching/testen";
        public const string Anwenden = "/api/matching/anwenden";
        public const string AutoStatus = "/api/matching/auto-status";
    }

    public static class Artikel
    {
        public const string Liste = "/api/artikel";
        public const string Einzeln = "/api/artikel/{id}";
    }

    public static class Stammartikel
    {
        /// <summary>GET, POST</summary>
        public const string Liste = "/api/stammartikel";
        /// <summary>GET, PUT</summary>
        public const string Einzeln = "/api/stammartikel/{id}";
        /// <summary>DELETE: Artikel aus dem Stammartikel lösen</summary>
        public const string Artikel = "/api/stammartikel/{id}/artikel/{artikelId}";
    }

    public static class Auswertung
    {
        public const string Dashboard = "/api/dashboard";
    }

    public static class Export
    {
        public const string Vorschau = "/api/export/vorschau";
        /// <summary>POST: Export erzeugen</summary>
        public const string Erzeugen = "/api/export";
        public const string Laeufe = "/api/export/laeufe";
        public const string LaufDatei = "/api/export/laeufe/{id}/datei";
    }

    public static class Einstellungen
    {
        public const string Alle = "/api/einstellungen";
        /// <summary>PUT; gruppe = allgemein|ust|pruefungen|di|mail|ordner|aufbewahrung|zugriff</summary>
        public const string Gruppe = "/api/einstellungen/{gruppe}";
        public const string DiVerbrauch = "/api/einstellungen/di/verbrauch";
        public const string DiVerbindungTesten = "/api/einstellungen/di/verbindung-testen";
        public const string DiFortsetzen = "/api/einstellungen/di/fortsetzen";
        public const string MailVerbindungTesten = "/api/einstellungen/mail/verbindung-testen";
        public const string MailJetztAbrufen = "/api/einstellungen/mail/jetzt-abrufen";
        public const string MailStatus = "/api/einstellungen/mail/status";
        /// <summary>PUT: IMAP-Zugang speichern – nur, wenn er nicht per .env gesetzt ist</summary>
        public const string MailZugang = "/api/einstellungen/mail/zugang";
    }

    public static class Benachrichtigungen
    {
        public const string Liste = "/api/benachrichtigungen";
        public const string Zaehler = "/api/benachrichtigungen/zaehler";
        public const string Gelesen = "/api/benachrichtigungen/{id}/gelesen";
        public const string AlleGelesen = "/api/benachrichtigungen/alle-gelesen";
    }

    public static class Aufbewahrung
    {
        public const string Loeschbar = "/api/aufbewahrung/loeschbar";
        public const string Loeschen = "/api/aufbewahrung/loeschen";
    }

    public static class Integritaet
    {
        public const string Laeufe = "/api/integritaet/laeufe";
        public const string Starten = "/api/integritaet/starten";
    }

    public static class SystemInfo
    {
        public const string Status = "/api/system/status";
        public const string Backup = "/api/system/backup";
    }

    public static class Suche
    {
        public const string Global = "/api/suche";
    }

    public static class Tokens
    {
        /// <summary>GET (eigene), POST</summary>
        public const string Liste = "/api/tokens";
        public const string Widerrufen = "/api/tokens/{id}/widerrufen";
    }

    public static class Verfahrensdoku
    {
        public const string Info = "/api/verfahrensdoku";
        public const string Pdf = "/api/verfahrensdoku/pdf";
    }
}
