import { createFileRoute, Outlet } from '@tanstack/react-router'

/** Layout des Beleg-Details (Viewer und Tabs folgen im Beleg-Detail-Ticket). */
export const Route = createFileRoute('/_app/belege/$belegId')({
  component: Outlet,
})
