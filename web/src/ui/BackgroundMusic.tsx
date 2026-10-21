import { useEffect, useSyncExternalStore } from 'react'
import { backgroundMusic, cityRaidPlaying } from '../game/backgroundMusic'
import { setMusicMuted, setMusicVolume, useMusicMuted, useMusicVolume } from '../gamePreferences'
import { useI18n } from '../i18n'
import type { MusicScene } from '../game/musicScene'

/** Map control plus the playback that follows the open village. */
export function BackgroundMusic({ battles, scene, inline = false }: {
  battles?: readonly { finishedAt: string | null; phase: string }[]
  scene?: MusicScene
  inline?: boolean
}) {
  const { language } = useI18n()
  const muted = useMusicMuted()
  const volume = useMusicVolume()
  const raid = cityRaidPlaying(battles)
  const ru = language === 'ru'
  const off = muted || volume <= 0
  const started = useSyncExternalStore(
    (listener) => backgroundMusic().subscribe(listener),
    () => { const snap = backgroundMusic().snapshot(); return snap.unlocked && !snap.paused && (snap.contextState === null || snap.contextState === 'running') },
    () => false,
  )

  useEffect(() => {
    const music = backgroundMusic()
    music.attach()
    return () => music.detach()
  }, [])

  useEffect(() => {
    backgroundMusic().apply({ raid, scene, muted, volume })
  }, [raid, scene, muted, volume])

  useEffect(() => {
    if (off) return
    const start = (event: PointerEvent) => {
      const target = event.target
      if (target instanceof Element && target.closest('.music-toggle')) return
      backgroundMusic().unlock()
    }
    window.addEventListener('pointerdown', start)
    return () => window.removeEventListener('pointerdown', start)
  }, [off])

  return (
    <button
      type="button"
      className={`music-toggle${inline ? ' music-inline' : ''}`}
      aria-pressed={!off}
      aria-label={off ? (ru ? 'Включить музыку деревни' : 'Turn village music on') : (ru ? 'Музыка деревни' : 'Village music')}
      title={off ? (ru ? 'Включить музыку' : 'Turn music on') : (ru ? 'Выключить музыку' : 'Turn music off')}
      onClick={() => {
        if (!started && !off) {
          backgroundMusic().unlock()
          return
        }
        const next = !off
        setMusicMuted(next)
        if (!next) {
          if (volume <= 0) setMusicVolume(0.4)
          backgroundMusic().unlock()
        }
      }}
    >
      <SpeakerIcon off={off} />
    </button>
  )
}

/** Volume remembered on this device. The map button is the quick mute. */
export function MusicSettings() {
  const { language } = useI18n()
  const volume = useMusicVolume()
  const muted = useMusicMuted()
  const ru = language === 'ru'
  const off = muted || volume <= 0
  return (
    <section className="card">
      <h3>{ru ? 'Музыка' : 'Music'}</h3>
      <label>
        <input
          type="checkbox"
          checked={!off}
          onChange={(event) => {
            const enabled = event.target.checked
            setMusicMuted(!enabled)
            if (enabled) {
              if (volume <= 0) setMusicVolume(0.4)
              backgroundMusic().unlock()
            }
          }}
        />
        {ru ? 'Фоновая музыка' : 'Background music'}
      </label>
      <label className="music-volume">
        <span>{ru ? 'Громкость' : 'Volume'} {Math.round(volume * 100)}%</span>
        <input
          type="range"
          min={0}
          max={100}
          step={5}
          value={Math.round(volume * 100)}
          aria-label={ru ? 'Громкость музыки' : 'Music volume'}
          onChange={(event) => {
            const next = Number(event.target.value) / 100
            setMusicVolume(next)
            setMusicMuted(next <= 0)
            if (next > 0) backgroundMusic().unlock()
          }}
        />
      </label>
      <p className="small muted">
        {ru
          ? 'В бою звучит боевая тема. После него продолжается спокойный плейлист.'
          : 'Combat uses the battle theme. Afterwards, the calm playlist resumes.'}
      </p>
    </section>
  )
}

function SpeakerIcon({ off }: { off: boolean }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M4 10v4h3l5 4V6L7 10H4z" />
      {off ? <path d="M16 9l5 6M21 9l-5 6" /> : <path d="M16 9.5a3 3 0 0 1 0 5M18.5 7a6 6 0 0 1 0 10" />}
    </svg>
  )
}
