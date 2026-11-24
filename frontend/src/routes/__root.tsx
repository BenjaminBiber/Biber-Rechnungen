import { createRootRouteWithContext, Outlet } from '@tanstack/react-router'
import { Platzhalter } from '@/components/Platzhalter'
import type { RouterKontext } from '@/router'

export const Route = createRootRouteWithContext<RouterKontext>()({
  component: Outlet,
  notFoundComponent: () => <Platzhalter titel="Seite nicht gefunden" />,
})
