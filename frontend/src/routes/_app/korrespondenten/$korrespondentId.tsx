import { createFileRoute } from '@tanstack/react-router'
import { useKorrespondent } from '@/api/korrespondenten'
import { detailSuche } from '@/features/korrespondenten/detail/gemeinsam'
import { KorrespondentDetailSeite } from '@/features/korrespondenten/detail/KorrespondentDetailSeite'

export const Route = createFileRoute('/_app/korrespondenten/$korrespondentId')({
  validateSearch: detailSuche,
  component: KorrespondentDetailRoute,
})

function KorrespondentDetailRoute() {
  const { korrespondentId } = Route.useParams()
  const { jahr = new Date().getFullYear(), logoAus } = Route.useSearch()
  const navigate = Route.useNavigate()
  const { data, isPending } = useKorrespondent(korrespondentId, jahr)
  return (
    <KorrespondentDetailSeite
      detail={data}
      istPending={isPending}
      jahr={jahr}
      logoAus={logoAus}
      onJahr={(j) => void navigate({ search: (alt) => ({ ...alt, jahr: j }), replace: true })}
      onLogoAusSchliessen={() => void navigate({ search: (alt) => ({ ...alt, logoAus: undefined }), replace: true })}
    />
  )
}
