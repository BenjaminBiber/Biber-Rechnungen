import { useNavigate } from '@tanstack/react-router'
import { useAbmeldenUndWeiter } from '@/components/shell/useAbmeldenUndWeiter'
import { useDarstellung } from '@/lib/theme'
import type { Befehl } from './befehle'

/** Führt einen Befehl aus: navigiert, wechselt die Darstellung oder meldet ab. */
export function useBefehlAusfuehren(): (befehl: Befehl) => void {
  const navigate = useNavigate()
  const [, setzeDarstellung] = useDarstellung()
  const abmelden = useAbmeldenUndWeiter()
  return (befehl) => {
    const ziel = befehl.ziel
    if (ziel.art === 'route') void navigate({ to: ziel.to, search: ziel.search } as never)
    else if (ziel.art === 'darstellung') setzeDarstellung(ziel.darstellung)
    else void abmelden()
  }
}
