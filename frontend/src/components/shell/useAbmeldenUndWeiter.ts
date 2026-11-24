import { useNavigate } from '@tanstack/react-router'
import { useAbmelden } from '@/api/auth'

/** Meldet ab und führt danach auf `/anmelden` (Benutzermenü und „Mehr“-Sheet). */
export function useAbmeldenUndWeiter(): () => Promise<void> {
  const abmelden = useAbmelden()
  const navigate = useNavigate()
  return async () => {
    await abmelden.mutateAsync()
    await navigate({ to: '/anmelden' })
  }
}
