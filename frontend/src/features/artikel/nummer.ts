/** „Pressnippel Megacr. (5G8FDLORX)“: gleichnamige Artikel eines Lieferanten unterscheiden sich nur über die Nummer. */
export function mitArtikelnummer(a: { name: string; artikelnummer?: string | null }): string {
  return a.artikelnummer ? `${a.name} (${a.artikelnummer})` : a.name
}
