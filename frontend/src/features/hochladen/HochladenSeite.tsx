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
  const { eintraege, hinzufuegen, entfernen, erneut } = useUploadWarteschlange()
  const laedt = eintraege.some((e) => e.status === 'laedt')

  useBlocker({
    shouldBlockFn: () => !window.confirm('Uploads laufen noch. Seite trotzdem verlassen?'),
    enableBeforeUnload: () => laedt,
    disabled: !laedt,
  })

  return (
    <div className="flex flex-col gap-5">
      <Seitenkopf titel="Hochladen & Import" />
      {darfBearbeiten ? (
        <div className="flex flex-wrap gap-5">
          <Dropzone onDateien={hinzufuegen} />
        </div>
      ) : (
        <p className="hinweis m-0">Mit deiner Rolle kannst du keine Belege hochladen.</p>
      )}
      <UploadListe eintraege={eintraege} onEntfernen={entfernen} onErneut={erneut} />
      <ImportVorgaenge />
    </div>
  )
}
