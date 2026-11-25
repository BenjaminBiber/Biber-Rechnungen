import { createRootRouteWithContext, Outlet } from '@tanstack/react-router'
import { Fehlerseite, SeiteNichtGefunden } from '@/components/fehler/Fehlerseiten'
import type { RouterKontext } from '@/router'

/** Wurzel: Fehlerseiten hier laufen ohne Shell (z. B. wenn der Server beim Start nicht antwortet). */
export const Route = createRootRouteWithContext<RouterKontext>()({
  component: Outlet,
  notFoundComponent: () => <SeiteNichtGefunden vollbild />,
  errorComponent: (props) => <Fehlerseite {...props} vollbild />,
})
