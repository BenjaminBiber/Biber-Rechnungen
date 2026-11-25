import { useBlocker } from '@tanstack/react-router'
import { Seitenkopf } from '@/components/Seitenkopf'
import { useRolle } from '@/components/shell/useRolle'
import { Dropzone } from './Dropzone'
import { ImportVorgaenge } from './ImportVorgaenge'
import { UploadListe } from './UploadListe'
import { useUploadWarteschlange } from './uploadWarteschlange'

/** Seite „Hochladen & Import“: Dropzone, Liste der Uploads dieser Sitzung und Import-Vorgänge aller Quellen. */
export function HochladenSeite() {
  const { darfBearbeiten } = useRolle()
  const { eintraege, hinzufuegen, entfernen, erneut, anmeldungNoetig } = useUploadWarteschlange()
  const laedt = eintraege.some((e) => e.status === 'laedt')
  const wartend = eintraege.filter((e) => e.status === 'wartet').length

  useBlocker({
    shouldBlockFn: () => !window.confirm('Uploads laufen noch. Seite trotzdem verlassen?'),
    enableBeforeUnload: () => laedt,
    disabled: !laedt,
  })

  return (
    <main className="seite flex flex-col gap-5">
      <Seitenkopf titel="Hochladen & Import" className="mb-0" />
      {anmeldungNoetig && wartend > 0 && (
        <p className="hinweis m-0" role="status">
          Bitte anmelden, um {wartend} wartende Uploads zu senden
        </p>
      )}
      {darfBearbeiten ? (
        <div className="flex flex-wrap gap-5">
          <Dropzone onDateien={hinzufuegen} />
        </div>
      ) : (
        <p className="hinweis m-0">Mit deiner Rolle kannst du keine Belege hochladen.</p>
      )}
      <UploadListe eintraege={eintraege} onEntfernen={entfernen} onErneut={erneut} />
      <ImportVorgaenge />
    </main>
  )
}
