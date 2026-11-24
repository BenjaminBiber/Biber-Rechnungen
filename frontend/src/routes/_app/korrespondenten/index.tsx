import { createFileRoute } from '@tanstack/react-router'
import { useCallback } from 'react'
import { korrespondentenTypen } from '@/features/korrespondenten/gemeinsam'
import { KorrespondentenSeite, type KorrespondentenWahl } from '@/features/korrespondenten/KorrespondentenSeite'

export const Route = createFileRoute('/_app/korrespondenten/')({
  validateSearch: (suche: Record<string, unknown>): KorrespondentenWahl => {
    const typ = korrespondentenTypen.find((t) => t === suche.typ)
    const text = typeof suche.suche === 'string' || typeof suche.suche === 'number' ? String(suche.suche) : ''
    const ids = typeof suche.zusammenfuehren === 'string' ? suche.zusammenfuehren : ''
    return {
      ...(text ? { suche: text } : {}),
      ...(typ ? { typ } : {}),
      ...(ids ? { zusammenfuehren: ids } : {}),
      ...(suche.neu === true || suche.neu === 'true' || suche.neu === 1 ? { neu: true } : {}),
    }
  },
  component: KorrespondentenRoute,
})

function KorrespondentenRoute() {
  const suche = Route.useSearch()
  const navigate = Route.useNavigate()
  const onWahl = useCallback(
    (wahl: KorrespondentenWahl) => void navigate({ search: (alt) => ({ ...alt, ...wahl }), replace: true }),
    [navigate],
  )
  return (
    <KorrespondentenSeite
      suche={suche.suche ?? ''}
      typ={suche.typ ?? 'Lieferanten'}
      zusammenfuehren={suche.zusammenfuehren?.split(',').filter(Boolean) ?? []}
      neu={suche.neu ?? false}
      jahr={new Date().getFullYear()}
      onWahl={onWahl}
    />
  )
}
