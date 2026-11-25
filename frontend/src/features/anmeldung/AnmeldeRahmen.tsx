import type { ReactNode } from 'react'
import bueroBild from '@/assets/anmeldung-buero.webp'
import { Logo } from '@/components/Logo'

/**
 * Layout der Anmeldung (Login.dc.html): links das Bürofoto mit Logo-Fahne, rechts eine weiße Spalte
 * von 560 px mit dem Formular. Unter 960 px rutscht das Foto als Band über das Formular.
 */
export function AnmeldeRahmen({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-wrap bg-karte font-sans text-text">
      <aside
        data-testid="anmelde-feld"
        className="relative flex min-h-[240px] flex-[1_1_480px] flex-col justify-between overflow-hidden bg-seitenleiste bg-cover bg-center bg-no-repeat"
        style={{ backgroundImage: `url(${bueroBild})` }}
      >
        <div
          aria-hidden="true"
          className="absolute inset-0 backdrop-grayscale-[0.65]"
          style={{
            background:
              'linear-gradient(180deg, rgba(52, 51, 79, 0.5) 0%, rgba(110, 112, 122, 0.18) 22%, rgba(110, 112, 122, 0.18) 100%)',
          }}
        />
        <div className="relative flex items-center gap-3 self-start bg-seitenleiste py-7 pr-14 pl-6 whitespace-nowrap sm:py-10 sm:pr-[72px] sm:pl-14 [clip-path:polygon(0_0,100%_0,100%_calc(100%_-_40px),calc(100%_-_40px)_100%,0_100%)]">
          <Logo className="h-8 w-[53px] text-white" />
          <span className="text-[22px] font-semibold text-white">Biber-Rechnungen</span>
        </div>
      </aside>
      <main className="relative z-[1] flex min-w-0 flex-[1_1_100%] flex-col bg-karte px-6 pt-8 pb-8 sm:px-16 sm:pt-10 min-[961px]:flex-[0_0_560px]">
        <div className="mx-auto flex w-full max-w-[432px] flex-1 flex-col justify-center gap-6">{children}</div>
      </main>
    </div>
  )
}
