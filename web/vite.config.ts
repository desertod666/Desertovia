import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'
import { resolve } from 'node:path'

// End-to-end tests run their own backend next to the development one.
const base = (process.env.VITE_APP_BASE ?? '/').replace(/\/?$/, '/')
const backend = process.env.DESERTOD_BACKEND ?? 'http://localhost:5080'

// changeOrigin stays false so the backend sees the Vite host; OAuth redirect URLs and cookies then use port 5190.
const proxied = { target: backend, changeOrigin: false }

export default defineConfig(({ command }) => {
  // React ships its production code only when NODE_ENV is exactly "production", and Vite keeps any NODE_ENV it
  // inherits, so a stray machine-wide value would otherwise put development React into the release bundle.
  if (command === 'build') process.env.NODE_ENV = 'production'

  return {
    base,
    // Generated .js files sit beside the TypeScript sources. Prefer the sources, or the music player never reaches the bundle.
    resolve: {
      extensions: ['.mjs', '.mts', '.ts', '.tsx', '.jsx', '.js', '.json'],
    },
    plugins: [
      react(),
      VitePWA({
        // A silent reload in the middle of a task would lose what the player was doing, so the player chooses when.
        registerType: 'prompt',
        includeAssets: ['favicon.ico', 'favicon.svg', 'apple-touch-icon-180x180.png'],
        manifest: {
          name: 'Desertovia',
          short_name: 'Desertovia',
          lang: 'en',
          start_url: base,
          scope: base,
          display: 'standalone',
          background_color: '#2a3a20',
          theme_color: '#2a3a20',
          icons: [
            { src: 'pwa-64x64.png', sizes: '64x64', type: 'image/png' },
            { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
            { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
            { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
          ],
        },
        workbox: {
          globPatterns: ['**/*.{js,css,html,ico,png,svg,webp}'],
          maximumFileSizeToCacheInBytes: 5 * 1024 * 1024,
          navigateFallback: base + 'index.html',
          // These paths are answered by the server: login redirects, OAuth callbacks, the API, SignalR and the probe.
          navigateFallbackDenylist: [/\/api\//, /\/auth\//, /\/signin-/, /\/hubs\//, /\/healthz/],
        },
      }),
    ],
    server: {
      port: 5190,
      strictPort: true,
      proxy: {
        '/api': proxied,
        '/auth': proxied,
        '/signin-twitch': proxied,
        '/signin-google': proxied,
        '/hubs': { ...proxied, ws: true },
      },
    },
    build: {
      outDir: '../src/Desertod.Api/wwwroot',
      emptyOutDir: true,
      ...(process.env.DESERTOD_E2E_HARNESS ? { rolldownOptions: { input: { main: resolve('index.html'), world:resolve('e2e/world-harness.html'), farm:resolve('e2e/farm-harness.html'), reconnect: resolve('e2e/reconnect-harness.html'), progression:resolve('e2e/progression-harness.html'), fences:resolve('e2e/fences-harness.html'), raidEditor: resolve('e2e/raid-editor-harness.html'), rescue: resolve('e2e/rescue-harness.html'), playerFeedback: resolve('e2e/player-feedback-harness.html'), ownerControls:resolve('e2e/owner-controls-harness.html'), post071: resolve('e2e/post-071-harness.html'), liveCombat: resolve('e2e/live-combat-harness.html'), lighting: resolve('e2e/harness.html'), chat: resolve('e2e/chat-harness.html'), hospital: resolve('e2e/hospital-harness.html'), cooperative: resolve('e2e/cooperative-harness.html'), patients: resolve('e2e/patients-harness.html'), raidTiming: resolve('e2e/raid-timing-harness.html'), almighty: resolve('e2e/almighty-harness.html'), economy: resolve('e2e/economy-harness.html'), mobileMenu: resolve('e2e/mobile-menu-harness.html'), sound: resolve('e2e/sound-harness.html'), dungeonCorpse: resolve('e2e/dungeon-corpse-harness.html') } } } : {}),
    },
  }
})
