import ruhebild from '@/assets/fehler/biber-fehler.png'
import reparieren from '@/assets/fehler/reparieren.webp'
import suchen from '@/assets/fehler/suchen.webp'
import warten from '@/assets/fehler/warten.webp'

const bilder = { suchen, reparieren, warten }

/** Wiederholt die Bewegung alle 4,8 Sekunden und zeigt bei Bewegungsreduktion das Ruhebild. */
export function BiberAnimation({ code, ton }: { code: string; ton: 'orange' | 'rot' | 'grau' }) {
  const bewegung = code === '404' || code === '410' ? 'suchen' : ton !== 'grau' ? 'reparieren' : 'warten'
  return (
    <picture className="ml-2 block w-[232px]">
      <source media="(prefers-reduced-motion: reduce)" srcSet={ruhebild} />
      <img src={bilder[bewegung]} alt="" width={222} height={178} className="block h-auto w-full" />
    </picture>
  )
}
