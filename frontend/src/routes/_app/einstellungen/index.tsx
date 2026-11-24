import { createFileRoute, redirect } from '@tanstack/react-router'

export const Route = createFileRoute('/_app/einstellungen/')({
  beforeLoad: () => {
    throw redirect({ to: '/einstellungen/pruefungen', replace: true })
  },
})
