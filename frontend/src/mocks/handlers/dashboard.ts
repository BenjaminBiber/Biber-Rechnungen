import { HttpResponse } from 'msw'
import type { Dashboard } from '@/api/typen'
import { db } from '../db'
import { istBezahlt, istZuPruefen } from './belege'
import { route, type Route } from './gemeinsam'

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
    diVerbrauch: db.diVerbrauch,
  }
}

export const dashboardRouten: Route[] = [
  route('get', '/api/dashboard', 'Lesen', () => HttpResponse.json(dashboard())),
  route('get', '/api/system/status', 'Lesen', () => HttpResponse.json(db.systemStatus)),
  route('get', '/api/system/backup', 'Lesen', () => HttpResponse.json(db.backup)),
]
