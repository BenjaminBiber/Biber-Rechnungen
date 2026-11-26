/// <reference types="node" />
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import * as artikel from './artikel'
import * as aufbewahrung from './aufbewahrung'
import * as auth from './auth'
import * as belege from './belege'
import * as benachrichtigungen from './benachrichtigungen'
import * as benutzer from './benutzer'
import * as dashboard from './dashboard'
import * as einstellungen from './einstellungen'
import * as exporte from './export'
import * as importe from './import'
import * as integritaet from './integritaet'
import * as konto from './konto'
import * as korrespondenten from './korrespondenten'
import * as suche from './suche'
import * as tags from './tags'
import * as tokens from './tokens'
import * as verfahrensdoku from './verfahrensdoku'

const module = {
  artikel,
  aufbewahrung,
  auth,
  belege,
  benachrichtigungen,
  benutzer,
  dashboard,
  einstellungen,
  export: exporte,
  import: importe,
  integritaet,
  konto,
  korrespondenten,
  suche,
  tags,
  tokens,
  verfahrensdoku,
}

/**
 * Genau ein Hook (bzw. URL-Helfer für Datei-Endpunkte und Browser-Weiterleitungen) je operationId.
 * `getAuthGoogleRueckruf` ruft Google selbst auf und braucht im Frontend nichts.
 */
const zuordnung: Record<string, `${keyof typeof module}.${string}` | null> = {
  getAuthStatus: 'auth.useAuthStatus',
  getAuthTotpVorschlag: 'auth.useTotpVorschlag',
  postAuthErsteinrichtung: 'auth.useErsteinrichtung',
  postAuthAnmelden: 'auth.useAnmelden',
  postAuthTotp: 'auth.useTotp',
  postAuthWiederherstellungscode: 'auth.useWiederherstellungscode',
  postAuthAbmelden: 'auth.useAbmelden',
  postAuthRegistrieren: 'auth.useRegistrieren',
  getAuthGoogleStart: 'auth.googleStartUrl',
  getAuthGoogleRueckruf: null,
  getKontoEigenes: 'konto.useKonto',
  putKontoEigenes: 'konto.useKontoAendern',
  postKontoPasswort: 'konto.usePasswortAendern',
  postKontoTotpEinrichten: 'konto.useTotpEinrichten',
  postKontoTotpAktivieren: 'konto.useTotpAktivieren',
  postKontoTotpDeaktivieren: 'konto.useTotpDeaktivieren',
  postKontoWiederherstellungscodes: 'konto.useWiederherstellungscodesErzeugen',
  postKontoAndereSitzungenAbmelden: 'konto.useAndereAbmelden',
  getBenutzerListe: 'benutzer.useBenutzer',
  postBenutzerListe: 'benutzer.useBenutzerAnlegen',
  putBenutzerEinzeln: 'benutzer.useBenutzerAendern',
  postBenutzerPasswort: 'benutzer.usePasswortZuruecksetzen',
  postBenutzerFreigeben: 'benutzer.useBenutzerFreigeben',
  postBenutzerAblehnen: 'benutzer.useBenutzerAblehnen',
  getBelegeListe: 'belege.useBelege',
  getBelegeZaehler: 'belege.useBelegZaehler',
  getBelegeEinzeln: 'belege.useBeleg',
  putBelegeEinzeln: 'belege.useBelegAendern',
  getBelegeNachbarn: 'belege.useBelegNachbarn',
  getBelegeAehnliche: 'belege.useAehnlicheBelege',
  getBelegeHistorie: 'belege.useBelegHistorie',
  getBelegeDatei: 'belege.belegDateiUrl',
  getBelegeVorschau: 'belege.belegVorschauUrl',
  getBelegeVisualisierung: 'belege.belegVisualisierungUrl',
  getAnsichtenListe: 'belege.useAnsichten',
  postAnsichtenListe: 'belege.useAnsichtSpeichern',
  deleteAnsichtenEinzeln: 'belege.useAnsichtLoeschen',
  putBelegePositionen: 'belege.usePositionenErsetzen',
  postBelegeTags: 'belege.useTagHinzufuegen',
  deleteBelegeTag: 'belege.useTagEntfernen',
  postBelegeBestaetigen: 'belege.useBelegBestaetigen',
  postBelegeErneutAuslesen: 'belege.useErneutAuslesen',
  postBelegeDublette: 'belege.useDubletteSetzen',
  postBelegeBezug: 'belege.useBezugSetzen',
  putBelegeSkonto: 'belege.useSkontoSetzen',
  postBelegeMassenaktion: 'belege.useMassenaktion',
  postBelegeAufteilen: 'belege.useAufteilen',
  postBelegeLogoUebernehmen: 'belege.useLogoUebernehmen',
  postImportUpload: 'import.useUpload',
  getImportVorgaenge: 'import.useImportVorgaenge',
  getImportVorgang: 'import.useImportVorgang',
  getKorrespondentenListe: 'korrespondenten.useKorrespondenten',
  postKorrespondentenListe: 'korrespondenten.useKorrespondentAnlegen',
  getKorrespondentenEinzeln: 'korrespondenten.useKorrespondent',
  putKorrespondentenEinzeln: 'korrespondenten.useKorrespondentAendern',
  postKorrespondentenZusammenfuehren: 'korrespondenten.useKorrespondentenZusammenfuehren',
  getKorrespondentenLogo: 'korrespondenten.korrespondentLogoUrl',
  putKorrespondentenLogo: 'korrespondenten.useLogoHochladen',
  deleteKorrespondentenLogo: 'korrespondenten.useLogoEntfernen',
  postKorrespondentenLogoFavicon: 'korrespondenten.useLogoFavicon',
  postKorrespondentenUstIdPruefen: 'korrespondenten.useUstIdPruefen',
  getTagsListe: 'tags.useTags',
  postTagsListe: 'tags.useTagAnlegen',
  putTagsEinzeln: 'tags.useTagAendern',
  deleteTagsEinzeln: 'tags.useTagLoeschen',
  getKategorienListe: 'tags.useKategorien',
  postKategorienListe: 'tags.useKategorieAnlegen',
  putKategorienEinzeln: 'tags.useKategorieAendern',
  deleteKategorienEinzeln: 'tags.useKategorieLoeschen',
  postMatchingTesten: 'tags.useMatchingTesten',
  postMatchingAnwenden: 'tags.useMatchingAnwenden',
  getMatchingAutoStatus: 'tags.useAutoMatchingStatus',
  getArtikelListe: 'artikel.useArtikel',
  getArtikelEinzeln: 'artikel.useArtikelDetail',
  getStammartikelListe: 'artikel.useStammartikel',
  postStammartikelListe: 'artikel.useStammartikelAnlegen',
  getStammartikelEinzeln: 'artikel.useStammartikelDetail',
  putStammartikelEinzeln: 'artikel.useStammartikelAendern',
  deleteStammartikelArtikel: 'artikel.useStammartikelArtikelEntfernen',
  getAuswertungDashboard: 'dashboard.useDashboard',
  getSystemInfoStatus: 'dashboard.useSystemStatus',
  getSystemInfoBackup: 'dashboard.useBackupStatus',
  postExportVorschau: 'export.useExportVorschau',
  postExportErzeugen: 'export.useExportErzeugen',
  getExportLaeufe: 'export.useExportLaeufe',
  getExportLaufDatei: 'export.exportDateiUrl',
  getEinstellungenAlle: 'einstellungen.useEinstellungen',
  putEinstellungenGruppe: 'einstellungen.useEinstellungenSpeichern',
  getEinstellungenDiVerbrauch: 'einstellungen.useDiVerbrauch',
  postEinstellungenDiVerbindungTesten: 'einstellungen.useDiVerbindungTesten',
  postEinstellungenDiFortsetzen: 'einstellungen.useDiFortsetzen',
  postEinstellungenMailVerbindungTesten: 'einstellungen.useMailVerbindungTesten',
  postEinstellungenMailJetztAbrufen: 'einstellungen.useMailJetztAbrufen',
  putEinstellungenMailZugang: 'einstellungen.useMailZugangSpeichern',
  getEinstellungenMailStatus: 'einstellungen.useMailStatus',
  getBenachrichtigungenListe: 'benachrichtigungen.useBenachrichtigungen',
  getBenachrichtigungenZaehler: 'benachrichtigungen.useBenachrichtigungZaehler',
  postBenachrichtigungenGelesen: 'benachrichtigungen.useBenachrichtigungGelesen',
  postBenachrichtigungenAlleGelesen: 'benachrichtigungen.useAlleBenachrichtigungenGelesen',
  getAufbewahrungLoeschbar: 'aufbewahrung.useLoeschbar',
  postAufbewahrungLoeschen: 'aufbewahrung.useAufbewahrungLoeschen',
  postBelegeLoeschen: 'aufbewahrung.useBelegLoeschen',
  getBelegeGeloeschte: 'aufbewahrung.useGeloeschteBelege',
  postBelegeWiederherstellen: 'aufbewahrung.useBelegWiederherstellen',
  getIntegritaetLaeufe: 'integritaet.useIntegritaetsLaeufe',
  postIntegritaetStarten: 'integritaet.useIntegritaetStarten',
  getSucheGlobal: 'suche.useSuche',
  getTokensListe: 'tokens.useTokens',
  postTokensListe: 'tokens.useTokenAnlegen',
  postTokensWiderrufen: 'tokens.useTokenWiderrufen',
  getVerfahrensdokuInfo: 'verfahrensdoku.useVerfahrensdoku',
  getVerfahrensdokuPdf: 'verfahrensdoku.verfahrensdokuPdfUrl',
}

type Vertrag = { paths: Record<string, Record<string, { operationId?: string }>> }
const vertrag = JSON.parse(
  readFileSync(resolve(import.meta.dirname, '../../../docs/api/openapi.json'), 'utf8'),
) as Vertrag
const operationIds = Object.values(vertrag.paths).flatMap((pfad) =>
  Object.values(pfad).flatMap((op) => (op.operationId ? [op.operationId] : [])),
)

describe('Hooks decken den API-Vertrag ab', () => {
  it('jede operationId ist genau einmal zugeordnet', () => {
    expect([...operationIds].sort()).toEqual(Object.keys(zuordnung).sort())
  })

  it('jeder zugeordnete Hook ist exportiert und eine Funktion', () => {
    const fehlend = Object.entries(zuordnung).flatMap(([operationId, ziel]) => {
      if (!ziel) return []
      const [modul, name] = ziel.split('.') as [keyof typeof module, string]
      return typeof (module[modul] as Record<string, unknown>)[name] === 'function' ? [] : [`${operationId} → ${ziel}`]
    })
    expect(fehlend).toEqual([])
  })

  it('kein Hook bedient zwei Endpunkte', () => {
    const ziele = Object.values(zuordnung).filter(Boolean)
    expect(ziele.filter((z, i) => ziele.indexOf(z) !== i)).toEqual([])
  })
})
