import type { ReactNode } from 'react'
import { Icon } from '@/components/Icon'

const stichpunkte: Array<[icon: string, text: string]> = [
  ['inbox', 'E-Mail, Scanner-Ordner und Upload in einer Pipeline'],
  ['fact_check', 'Automatische Plausibilitätsprüfung inkl. E-Rechnung (KoSIT)'],
  ['lock', 'GoBD-konforme Ablage: unveränderbar, protokolliert, 8 Jahre gesperrt'],
]

/** Zweispaltiges Layout der Anmeldung: Indigo-Feld (ab 1024 px) links, Formularkarte rechts. */
export function AnmeldeRahmen({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen font-sans text-text">
      <aside
        data-testid="anmelde-feld"
        className="hidden flex-[1_1_420px] flex-col justify-between gap-10 bg-seitenleiste p-14 text-[#d7d6ee] lg:flex"
      >
        <div className="flex items-center gap-3">
          <span className="flex h-11 w-11 items-center justify-center rounded-[11px] bg-karte text-[22px] font-bold text-akzent-text">
            B
          </span>
          <span className="text-xl font-semibold text-white">Biber-Rechnungen</span>
        </div>
        <div className="max-w-[440px]">
          <p className="m-0 mb-4 text-[34px] leading-tight font-semibold text-white">
            Belege rein, sauber abgelegt, auffindbar.
          </p>
          <ul className="m-0 flex list-none flex-col gap-3.5 p-0 text-[15px] leading-normal">
            {stichpunkte.map(([icon, text]) => (
              <li key={icon} className="flex gap-2.5">
                <Icon name={icon} className="text-[#b3b1f0]" />
                {text}
              </li>
            ))}
          </ul>
        </div>
        <div className="text-[13px] text-[#aeadd8]">Nur im lokalen Netz erreichbar · {window.location.host}</div>
      </aside>
      <main className="flex flex-[999_1_480px] items-center justify-center bg-flaeche px-6 py-12">
        <div className="flex w-full max-w-[420px] flex-col gap-5 rounded-2xl border border-rand bg-karte p-9">
          {children}
        </div>
      </main>
    </div>
  )
}
