import { describe, expect, it, vi } from 'vitest'
import { useNachAbschlussNeuLaden } from './belege'
import { renderMitQuery } from './testHilfe'

describe('useNachAbschlussNeuLaden', () => {
  it('lädt Belegabhängiges neu, sobald weniger Vorgänge laufen – sonst nicht', () => {
    let laufend: number | undefined = undefined
    const { rerender, queryClient } = renderMitQuery(() => useNachAbschlussNeuLaden(laufend))
    const invalidieren = vi.spyOn(queryClient, 'invalidateQueries')

    for (const wert of [2, 2, 3]) {
      laufend = wert
      rerender()
    }
    expect(invalidieren).not.toHaveBeenCalled()

    laufend = 1
    rerender()
    expect(invalidieren).toHaveBeenCalledWith({ queryKey: ['belege'] })
    expect(invalidieren).toHaveBeenCalledWith({ queryKey: ['dashboard'] })

    invalidieren.mockClear()
    laufend = undefined
    rerender()
    laufend = 0
    rerender()
    expect(invalidieren).not.toHaveBeenCalled()
  })
})
