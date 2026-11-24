import type { RequestHandler } from 'msw'

/**
 * MSW-Handler für Mock-Betrieb und Tests. Das API-Client-Ticket und die View-Tickets
 * ergänzen hier je Bereich ihre Handler (Fixtures = Beispieldaten des Designs).
 */
export const handlers: RequestHandler[] = []
