import { useSyncExternalStore } from 'react'
import { Icon } from '@/components/Icon'
import { cn } from '@/lib/utils'
import { abonnieren, aktuelleToasts, entfernen, type Toast } from './toast-store'

const icons = { erfolg: 'check_circle', info: 'info', fehler: 'error' } as const

function ToastEintrag({ toast }: { toast: Toast }) {
  const fehler = toast.art === 'fehler'
  return (
    <div
      role={fehler ? 'alert' : 'status'}
      className={cn(
        'pointer-events-auto flex items-center gap-3 rounded-[10px] px-4 py-3 text-sm',
        fehler
          ? 'border border-[#f5c2bd] bg-rot-flaeche text-[#8f1b12]'
          : 'bg-akzent text-auf-akzent shadow-[0_12px_30px_rgba(0,0,0,0.3)]',
      )}
    >
      <Icon name={icons[toast.art]} className={cn(toast.art === 'erfolg' && 'text-[#9fd8b8]')} />
      <span className="flex-1">{toast.text}</span>
      {toast.aktion && (
        <button
          type="button"
          className={cn('ml-2 cursor-pointer border-0 bg-transparent p-0 font-semibold', !fehler && 'text-[#b3b1f0]')}
          onClick={() => {
            toast.aktion!.ausfuehren()
            entfernen(toast.id)
          }}
        >
          {toast.aktion.text}
        </button>
      )}
      <button
        type="button"
        aria-label="Meldung schließen"
        className="ml-1 flex cursor-pointer border-0 bg-transparent p-0 text-inherit opacity-70"
        onClick={() => entfernen(toast.id)}
      >
        <Icon name="close" className="text-[18px]" />
      </button>
    </div>
  )
}

/** Stapel der Meldungen unten rechts; einmal im Layout eingebunden. */
export function ToastBereich() {
  const toasts = useSyncExternalStore(abonnieren, aktuelleToasts, aktuelleToasts)
  return (
    <div className="pointer-events-none fixed right-4 bottom-24 z-[60] flex max-w-[calc(100vw-32px)] flex-col gap-2 lg:bottom-6">
      {toasts.map((t) => (
        <ToastEintrag key={t.id} toast={t} />
      ))}
    </div>
  )
}
