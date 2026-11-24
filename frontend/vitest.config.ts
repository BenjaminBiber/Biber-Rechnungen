import { defineConfig, mergeConfig } from 'vitest/config'
import viteConfig from './vite.config.ts'

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      include: ['src/**/*.test.{ts,tsx}'],
      css: false,
      restoreMocks: true,
      // Seitentests rendern ganze Routen; unter Last (parallele Worktrees) reichen 5 s nicht immer.
      testTimeout: 15_000,
      hookTimeout: 15_000,
      // Höchstens die Hälfte der Kerne: mit allen Kernen verhungern einzelne jsdom-Worker.
      maxWorkers: '50%',
    },
  }),
)
