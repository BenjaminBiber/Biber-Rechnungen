import { createFileRoute, Outlet } from '@tanstack/react-router'

/** Layout der Einstellungen (Bereichsnavigation folgt im Einstellungen-Ticket). */
export const Route = createFileRoute('/_app/einstellungen')({
  component: Outlet,
})
