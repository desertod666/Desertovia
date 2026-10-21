import { defineConfig, devices } from '@playwright/test'

/** WebKit on Windows omits Web Audio. Linux CI exercises the real graph and decoded MP3 signal. */
export default defineConfig({
  testDir: './e2e',
  testMatch: 'sound.spec.ts',
  workers: 1,
  fullyParallel: false,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  forbidOnly: !!process.env.CI,
  retries: 0,
  outputDir: 'test-results/audio-webkit',
  reporter: [['list'], ['json', { outputFile: 'test-results/audio-webkit-results.json' }]],
  use: { baseURL: 'http://127.0.0.1:5192', locale: 'en-US', trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  projects: [{ name: 'webkit-audio', use: { ...devices['Desktop Safari'] } }],
  webServer: {
    command: 'npx vite build --outDir dist/audio --emptyOutDir --logLevel warn && npx vite preview --outDir dist/audio --host 127.0.0.1 --port 5192 --strictPort',
    url: 'http://127.0.0.1:5192/e2e/sound-harness.html',
    timeout: 120_000,
    reuseExistingServer: false,
    env: { DESERTOD_E2E_HARNESS: '1', VITE_DESERTOD_E2E: '1' },
  },
})
