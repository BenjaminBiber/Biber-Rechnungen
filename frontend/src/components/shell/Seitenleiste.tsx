import { Link } from '@tanstack/react-router'
import { useBelegZaehler } from '@/api/belege'
import { Icon } from '@/components/Icon'
import { navGruppen } from './navigation'

/** Indigo-Seitenleiste ab 1024 px (Sidebar.dc.html); der aktive Eintrag kommt vom Router. */
export function Seitenleiste() {
  const { data: zaehler } = useBelegZaehler()
  const inbox = zaehler?.inbox ?? 0

  return (
    <nav
      aria-label="Hauptnavigation"
      className="flex min-h-full w-[232px] flex-none flex-col gap-[22px] bg-seitenleiste px-3.5 py-5 text-white"
    >
      <Link to="/" className="flex items-center gap-2.5 px-2 py-1 no-underline">
        <span className="flex h-[34px] w-[34px] items-center justify-center rounded-[9px] bg-white text-[17px] font-bold text-akzent">
          B
        </span>
        <span className="flex flex-col">
          <span className="text-[15px] leading-tight font-semibold text-white">Biber-Rechnungen</span>
          <span className="text-xs text-white">Belegarchiv</span>
        </span>
      </Link>
      {navGruppen.map((gruppe) => (
        <div key={gruppe.label} className="flex flex-col gap-0.5">
          <div className="px-3 pb-1.5 text-[11px] font-semibold tracking-[0.08em] text-[#a9a8d6] uppercase">
            {gruppe.label}
          </div>
          {gruppe.eintraege.map((e) => (
            <Link
              key={e.id}
              to={e.to}
              activeOptions={{ exact: e.to === '/' }}
              className="flex h-10 items-center gap-3 rounded-lg px-3 text-sm font-medium text-white no-underline hover:bg-white/10 data-[status=active]:bg-akzent-aktiv"
            >
              <Icon name={e.icon} className="text-[20px]" />
              <span className="flex-1 text-white">{e.label}</span>
              {e.id === 'belege' && inbox > 0 && (
                <span
                  className="inline-flex h-5 min-w-[22px] items-center justify-center rounded-[10px] bg-[#5654a8] px-1.5 text-xs font-semibold text-white"
                  aria-label={`${inbox} in der Inbox`}
                >
                  {inbox}
                </span>
              )}
            </Link>
          ))}
        </div>
      ))}
    </nav>
  )
}
