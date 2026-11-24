import { useState, type DragEvent } from 'react'
import { Icon } from '@/components/Icon'
import { erlaubteEndungen } from './pruefung'

/** Große Ablagefläche: Klick, Tastatur (Enter/Leertaste auf dem Feld) oder Drag & Drop. */
export function Dropzone({ onDateien }: { onDateien: (dateien: File[]) => void }) {
  const [ziehen, setZiehen] = useState(false)

  const ablegen = (ereignis: DragEvent) => {
    ereignis.preventDefault()
    setZiehen(false)
    const dateien = Array.from(ereignis.dataTransfer.files)
    if (dateien.length) onDateien(dateien)
  }

  return (
    <label
      data-testid="dropzone"
      data-ziehen={ziehen || undefined}
      onDragEnter={(e) => {
        e.preventDefault()
        setZiehen(true)
      }}
      onDragOver={(e) => {
        e.preventDefault()
        setZiehen(true)
      }}
      onDragLeave={() => setZiehen(false)}
      onDrop={ablegen}
      className={`relative flex min-h-[240px] flex-[2_1_480px] cursor-pointer flex-col items-center justify-center gap-2.5 rounded-[14px] border-2 border-dashed p-8 text-center focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-akzent ${
        ziehen ? 'border-akzent bg-blau-flaeche' : 'border-akzent/40 bg-akzent-zart'
      }`}
    >
      <span className="flex h-[60px] w-[60px] items-center justify-center rounded-full bg-blau-flaeche text-akzent-text">
        <Icon name="cloud_upload" className="text-[32px]" />
      </span>
      <span className="text-lg font-semibold">Dateien hierher ziehen</span>
      <span className="gedaempft text-sm">
        oder <span className="font-medium text-akzent-text">Dateien auswählen</span> · mehrere gleichzeitig möglich
      </span>
      <span className="flex flex-wrap justify-center gap-1.5">
        <span className="chip chip-grau">PDF</span>
        <span className="chip chip-grau">ZUGFeRD / Factur-X</span>
        <span className="chip chip-grau">XRechnung (XML)</span>
        <span className="chip chip-grau">PNG · JPG · TIFF</span>
      </span>
      <input
        type="file"
        multiple
        accept={erlaubteEndungen.join(',')}
        aria-label="Dateien auswählen"
        className="absolute h-px w-px opacity-0"
        onChange={(e) => {
          const dateien = Array.from(e.target.files ?? [])
          e.target.value = ''
          if (dateien.length) onDateien(dateien)
        }}
      />
    </label>
  )
}
