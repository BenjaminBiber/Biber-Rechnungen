import { useState } from 'react'
import { Icon } from '@/components/Icon'

/**
 * Sichtbarkeit eines Passwortfelds: liefert den `type` fürs Eingabefeld und den Auge-Knopf dazu.
 * Für Felder, die kein Passwort sind, bleibt der `type` unverändert und es gibt keinen Knopf.
 */
export function usePasswortAnzeigen(type: string | undefined, feldname: string) {
  const [sichtbar, setSichtbar] = useState(false)
  const istPasswort = type === 'password'
  return {
    type: istPasswort && sichtbar ? 'text' : type,
    knopf: istPasswort ? (
      <PasswortKnopf sichtbar={sichtbar} feldname={feldname} onUmschalten={() => setSichtbar((s) => !s)} />
    ) : null,
  }
}

function PasswortKnopf({
  sichtbar,
  feldname,
  onUmschalten,
}: {
  sichtbar: boolean
  feldname: string
  onUmschalten: () => void
}) {
  return (
    <button
      type="button"
      onClick={onUmschalten}
      aria-pressed={sichtbar}
      aria-label={`${feldname} ${sichtbar ? 'verbergen' : 'anzeigen'}`}
      title={sichtbar ? 'Passwort verbergen' : 'Passwort anzeigen'}
      className="inline-flex size-[34px] flex-none cursor-pointer items-center justify-center rounded-[7px] border-0 bg-transparent text-gedaempft hover:bg-hover hover:text-text focus-visible:outline-2 focus-visible:outline-akzent"
    >
      <Icon name={sichtbar ? 'visibility_off' : 'visibility'} className="text-[20px] [vertical-align:0]" />
    </button>
  )
}
