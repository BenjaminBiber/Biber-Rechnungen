import { createFileRoute } from '@tanstack/react-router'
import { HochladenSeite } from '@/features/hochladen/HochladenSeite'
import { MobilHochladen } from '@/features/mobil/MobilHochladen'
import { useIstMobil } from '@/lib/useIstMobil'

/** `?geteilt=<Anzahl>`: Ziel des Share-Targets, nachdem der Service Worker die Dateien abgelegt hat. */
export const Route = createFileRoute('/_app/hochladen')({
  validateSearch: (suche: Record<string, unknown>): { geteilt?: number } =>
    suche.geteilt === undefined ? {} : { geteilt: Number(suche.geteilt) || 0 },
  component: Hochladen,
})

function Hochladen() {
  return useIstMobil() ? <MobilHochladen /> : <HochladenSeite />
}
