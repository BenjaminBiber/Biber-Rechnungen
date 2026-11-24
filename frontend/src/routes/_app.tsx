import { createFileRoute, Outlet } from '@tanstack/react-router'

/** Pfadloses Layout der angemeldeten Anwendung (Seitenleiste, Kopfleiste folgen im Layout-Ticket). */
export const Route = createFileRoute('/_app')({
  component: Outlet,
})
