import { createFileRoute } from '@tanstack/react-router'
import { useCallback } from 'react'
import { reiterListe } from '@/features/tags/gemeinsam'
import { TagsSeite, type TagsWahl } from '@/features/tags/TagsSeite'

export const Route = createFileRoute('/_app/tags')({
  validateSearch: (suche: Record<string, unknown>): TagsWahl => {
    const reiter = reiterListe.find((r) => r === suche.reiter)
    const bearbeiten = typeof suche.bearbeiten === 'string' ? suche.bearbeiten : ''
    return {
      ...(reiter ? { reiter } : {}),
      ...(bearbeiten ? { bearbeiten } : {}),
      ...(suche.neu === true || suche.neu === 'true' || suche.neu === 1 ? { neu: true } : {}),
    }
  },
  component: TagsRoute,
})

function TagsRoute() {
  const suche = Route.useSearch()
  const navigate = Route.useNavigate()
  const onWahl = useCallback(
    (wahl: TagsWahl) => void navigate({ search: (alt) => ({ ...alt, ...wahl }), replace: true }),
    [navigate],
  )
  return (
    <TagsSeite
      reiter={suche.reiter ?? 'tags'}
      bearbeiten={suche.bearbeiten}
      neu={suche.neu ?? false}
      jahr={new Date().getFullYear()}
      onWahl={onWahl}
    />
  )
}
