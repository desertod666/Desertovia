import { useState } from 'react'
import { createRoot } from 'react-dom/client'
import { cityRaidPlaying, backgroundMusic } from '../src/game/backgroundMusic'
import { I18nProvider } from '../src/i18n'
import { BackgroundMusic, MusicSettings } from '../src/ui/BackgroundMusic'
import '../src/index.css'
import type { MusicScene } from '../src/game/musicScene'

const stand = window as unknown as {
  villageMusic: {
    snapshot: () => ReturnType<ReturnType<typeof backgroundMusic>['snapshot']>
    cityRaidPlaying: typeof cityRaidPlaying
  }
}
stand.villageMusic = {
  snapshot: () => backgroundMusic().snapshot(),
  cityRaidPlaying,
}

function Stand() {
  const [phase, setPhase] = useState<string | null>(null)
  const [scene, setScene] = useState<MusicScene | undefined>(undefined)
  const [mount, setMount] = useState(0)
  const [shown, setShown] = useState(true)
  const battles = phase === null ? [] : [{ finishedAt: phase === 'Finished' ? '2026-10-18T00:00:00Z' : null, phase }]
  return (
    <I18nProvider>
      <div className="game-root">
        {shown && <BackgroundMusic key={mount} battles={battles} scene={scene} />}
        <button type="button" style={{ position: 'absolute', zIndex: 50, left: 8, top: 8 }} onClick={() => { setScene(undefined); setPhase((current) => current === 'Fighting' ? null : 'Fighting') }}>
          raid
        </button>
        <div style={{ position: 'absolute', zIndex: 30, left: 12, top: 48, width: 'min(320px, calc(100% - 70px))' }}>
          <MusicSettings />
          <label>Context <select aria-label="Music context" value={scene ?? ''} onChange={e => setScene((e.target.value || undefined) as MusicScene | undefined)}>
            <option value="">village</option><option value="M06">night</option><option value="farmDay">farm</option>
            <option value="M07">road</option><option value="M09">dungeon</option><option value="M11">skirmish</option>
          </select></label>
          <button type="button" onClick={() => setMount(n => n + 1)}>remount</button>
          <button type="button" onClick={() => setShown(v => !v)}>leave / return</button>
        </div>
      </div>
    </I18nProvider>
  )
}

createRoot(document.getElementById('app') ?? document.body).render(<Stand />)
