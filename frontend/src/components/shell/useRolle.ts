import { useAuthStatus } from '@/api/auth'
import type { Rolle } from '@/api/typen'

/**
 * Rolle des angemeldeten Benutzers. Solange der Status lädt, gilt die vorsichtigste Rolle „NurLesen“,
 * damit Bearbeiten-Aktionen nicht kurz aufblitzen.
 */
export function useRolle(): { rolle: Rolle; darfBearbeiten: boolean; istAdmin: boolean } {
  const { data } = useAuthStatus()
  const rolle: Rolle = data?.benutzer?.rolle ?? 'NurLesen'
  return { rolle, darfBearbeiten: rolle !== 'NurLesen', istAdmin: rolle === 'Admin' }
}
