import { createFileRoute } from '@tanstack/react-router'
import { useArtikelDetail } from '@/api/artikel'
import { detailSuche } from '@/features/artikel/detail/gemeinsam'
import { PreisDetailSeite } from '@/features/artikel/detail/PreisDetailSeite'

export const Route = createFileRoute('/_app/artikel/$artikelId')({
  validateSearch: detailSuche,
  component: ArtikelDetailRoute,
})

function ArtikelDetailRoute() {
  const { artikelId } = Route.useParams()
  const { jahr = new Date().getFullYear(), preisart } = Route.useSearch()
  const navigate = Route.useNavigate()
  const { data, isPending } = useArtikelDetail(artikelId, jahr)
  return (
    <PreisDetailSeite
      detail={data}
      istPending={isPending}
      jahr={jahr}
      preisart={preisart}
      onPreisart={(p) => void navigate({ search: (alt) => ({ ...alt, preisart: p }), replace: true })}
    />
  )
}
