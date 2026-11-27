import { HttpResponse } from 'msw'
import type { Dashboard, SkontoUebersicht } from '@/api/typen'
import { db } from '../db'
import { mockHeute } from '../fixtures/belege'
import { istBezahlt, istZuPruefen } from './belege'
import { route, runden, type Route } from './gemeinsam'

/** Offene Rechnungen mit Skontofrist ab heute, die nächste Frist zuerst (wie `AuswertungsDienst.SkontoAsync`). */
function skonto(): SkontoUebersicht {
  const laufend = db.belege
    .flatMap((b) =>
      b.belegart === 'Rechnung' && !istBezahlt(b) && !b.istDublette && b.skonto?.bis && b.skonto.bis >= mockHeute
        ? [{ beleg: b, skonto: { ...b.skonto, bis: b.skonto.bis } }]
        : [],
    )
    .sort((a, b) => a.skonto.bis.localeCompare(b.skonto.bis))
  return {
    anzahl: laufend.length,
    ersparnis: runden(laufend.reduce((summe, { skonto }) => summe + (skonto.betrag ?? 0), 0)),
    eintraege: laufend.slice(0, 4).map(({ beleg, skonto }) => ({
      belegId: beleg.id,
      korrespondent: beleg.korrespondent?.name ?? null,
      rechnungsnummer: beleg.rechnungsnummer ?? null,
      brutto: beleg.brutto ?? null,
      prozent: skonto.prozent,
      bis: skonto.bis,
      betrag: skonto.betrag ?? null,
    })),
  }
}

/**
 * Dashboard des Designs (`Main.dc.html`). Die Listen „Zu prüfen“ und „Offen“ folgen dem Mock-Zustand,
 * damit ein bestätigter oder bezahlter Beleg dort verschwindet; Kennzahlen und Diagramme bleiben wie im Design.
 */
function dashboard(): Dashboard {
  const zuPruefen = new Set(db.belege.filter(istZuPruefen).map((b) => b.id))
  const bezahlt = new Set(db.belege.filter(istBezahlt).map((b) => b.id))
  const zuPruefenListe = db.dashboard.zuPruefenListe.filter((e) => zuPruefen.has(e.belegId))
  const offeneBelege = db.dashboard.offeneBelege.filter((e) => !bezahlt.has(e.belegId))
  return {
    ...db.dashboard,
    zuPruefen: { ...db.dashboard.zuPruefen, anzahl: zuPruefenListe.length },
    zuPruefenListe,
    offeneBelege,
    skonto: skonto(),
    diVerbrauch: db.diVerbrauch,
  }
}

export const dashboardRouten: Route[] = [
  route('get', '/api/dashboard', 'Lesen', () => HttpResponse.json(dashboard())),
  route('get', '/api/system/status', 'Lesen', () => HttpResponse.json(db.systemStatus)),
  route('get', '/api/system/backup', 'Lesen', () => HttpResponse.json(db.backup)),
]
