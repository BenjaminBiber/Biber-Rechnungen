import type { RequestHandler } from 'msw'
import { artikelRouten } from './handlers/artikel'
import { authRouten } from './handlers/auth'
import { belegeRouten } from './handlers/belege'
import { benutzerRouten } from './handlers/benutzer'
import { dashboardRouten } from './handlers/dashboard'
import { einstellungenRouten } from './handlers/einstellungen'
import { exportRouten } from './handlers/export'
import { zuHandlern, type Route } from './handlers/gemeinsam'
import { importRouten } from './handlers/import'
import { kontoRouten } from './handlers/konto'
import { korrespondentenRouten } from './handlers/korrespondenten'
import { systemRouten } from './handlers/system'
import { tagsRouten } from './handlers/tags'

/**
 * Alle Endpunkte aus `docs/api/openapi.json` (geprüft von `abdeckung.test.ts`). Feste Pfade stehen vor
 * Pfaden mit Parametern, damit z. B. `/api/belege/zaehler` nicht als `/api/belege/{id}` gilt.
 */
export const routen: Route[] = [
  ...authRouten,
  ...kontoRouten,
  ...benutzerRouten,
  ...belegeRouten,
  ...importRouten,
  ...korrespondentenRouten,
  ...tagsRouten,
  ...artikelRouten,
  ...dashboardRouten,
  ...exportRouten,
  ...einstellungenRouten,
  ...systemRouten,
]

/**
 * MSW-Handler für Mock-Betrieb und Tests (Fixtures = Beispieldaten des Designs, Zustand in `db.ts`).
 * View-Tickets überschreiben Antworten nur lokal im Test mit `server.use(…)`.
 */
export const handlers: RequestHandler[] = zuHandlern(routen)
