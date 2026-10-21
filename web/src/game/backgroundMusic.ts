import { MUSIC_SCENES, MusicSceneState, type MusicScene } from './musicScene'
export { cityRaidPlaying } from './musicContext'

/** One controller and one audio element across village/dungeon navigation. Existing files are fallback beds. */
const CALM = ['part_001.mp3', 'part_002.mp3', 'part_003.mp3', 'part_004.mp3', 'part_005.mp3', 'part_006.mp3', 'part_007.mp3']
const BATTLE = 'battle.mp3'

export interface MusicSnapshot {
  src: string
  paused: boolean
  loop: boolean
  mode: 'calm' | 'battle'
  raid: boolean
  unlocked: boolean
  readyState: number
  error: number
  scene: MusicScene
  score: 'legacy-fallback'
  outputVolume: number
  volumeBackend: 'web-audio' | 'media-element'
  contextState: AudioContextState | null
}

class BackgroundMusic {
  private readonly audio = new Audio()
  private readonly calm: string[]
  private readonly battle: string
  private cursor = 0
  private calmTime = 0
  private mode: 'calm' | 'battle' = 'calm'
  private raid = false
  private muted = false
  private volume = 0.4
  private unlocked = false
  private users = 0
  private misses = 0
  private token = 0
  private readonly scenes = new MusicSceneState()
  private scene: MusicScene = 'M02'
  private transition: ReturnType<typeof setTimeout> | undefined
  private context: AudioContext | null = null
  private gain: GainNode | null = null
  private volumeUnavailable = false
  private readonly listeners = new Set<() => void>()

  private notify() {
    for (const listener of this.listeners) listener()
  }

  constructor() {
    const start = Math.floor(Math.random() * CALM.length)
    const order = CALM.slice(start).concat(CALM.slice(0, start))
    this.calm = order.map((file) => this.fileUrl(file))
    this.battle = this.fileUrl(BATTLE)
    this.audio.preload = 'none'
    this.audio.addEventListener('ended', this.onEnded)
    this.audio.addEventListener('playing', () => { this.misses = 0; this.notify() })
    this.audio.addEventListener('pause', () => this.notify())
    this.audio.addEventListener('error', this.onError)
    document.addEventListener('visibilitychange', this.onVisibility)
  }

  attach() {
    this.users += 1
    this.updateScene()
    if (this.unlocked && !this.silent()) void this.follow()
  }

  detach() {
    this.users = Math.max(0, this.users - 1)
    if (this.users === 0) {
      clearTimeout(this.transition)
      this.transition = undefined
      this.audio.pause()
    }
  }

  subscribe(listener: () => void) {
    this.listeners.add(listener)
    return () => { this.listeners.delete(listener) }
  }

  /** Must run inside a user gesture the first time, so the browser allows playback. */
  unlock() {
    this.prepareVolume()
    this.resumeContext()
    this.unlocked = true
    this.notify()
    if (!this.silent()) void this.follow()
  }

  apply(options: { raid?: boolean; scene?: MusicScene; muted: boolean; volume: number }) {
    this.muted = options.muted
    this.volume = Number.isFinite(options.volume) ? Math.min(1, Math.max(0, options.volume)) : 0
    this.scenes.request(options.scene ?? (options.raid ? 'M13' : 'M02'), performance.now())
    this.updateScene()
    this.applyVolume()
    if (!this.unlocked) return
    if (this.silent()) {
      this.audio.pause()
      return
    }
    void this.follow()
  }

  snapshot(): MusicSnapshot {
    return {
      src: this.audio.currentSrc || this.audio.src,
      paused: this.audio.paused,
      loop: this.audio.loop,
      mode: this.mode,
      raid: this.raid,
      unlocked: this.unlocked,
      readyState: this.audio.readyState,
      error: this.audio.error?.code ?? 0,
      scene: this.scene,
      score: 'legacy-fallback',
      outputVolume: this.gain?.gain.value ?? this.audio.volume,
      volumeBackend: this.gain ? 'web-audio' : 'media-element',
      contextState: this.context?.state ?? null,
    }
  }

  private fileUrl(file: string) {
    return `${import.meta.env.BASE_URL}assets/music/${file}`
  }

  private level() {
    return this.muted ? 0 : this.volume
  }

  /** iOS does not reliably honor HTMLMediaElement.volume. One owned GainNode controls the actual output. */
  private prepareVolume() {
    if (this.context || this.volumeUnavailable) return
    const Context = window.AudioContext ?? (window as Window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
    if (!Context) { this.volumeUnavailable = true; return }
    let context: AudioContext | null = null
    try {
      context = new Context()
      const gain = context.createGain()
      gain.gain.value = this.level()
      gain.connect(context.destination)
      context.createMediaElementSource(this.audio).connect(gain)
      this.context = context
      this.gain = gain
      this.audio.volume = 1 // Do not multiply the chosen volume twice on desktop browsers.
      context.addEventListener('statechange', () => this.notify())
    } catch {
      if (context) void context.close().catch(() => undefined)
      this.volumeUnavailable = true
    }
  }

  private resumeContext() {
    if (this.context && this.context.state !== 'running' && this.context.state !== 'closed')
      void this.context.resume().catch(() => this.notify())
  }

  private applyVolume() {
    if (this.gain && this.context) {
      const now = this.context.currentTime
      this.gain.gain.cancelScheduledValues(now)
      this.gain.gain.setTargetAtTime(this.level(), now, 0.015)
    } else this.audio.volume = this.level()
  }

  private silent() {
    return this.muted || this.volume <= 0 || this.users === 0 || document.hidden
  }

  private calmUrl() {
    return this.calm[this.cursor] ?? this.calm[0]
  }

  private updateScene() {
    clearTimeout(this.transition)
    const scene = this.scenes.tick(performance.now()) ?? this.scene
    const changed = scene !== this.scene
    this.scene = scene
    this.raid = scene === 'M13' || scene === 'M15'
    const due = this.scenes.nextChangeAt()
    this.transition = due !== null && this.users > 0 ? setTimeout(() => {
      this.updateScene()
      if (this.unlocked && !this.silent()) void this.follow()
    }, Math.max(1, due - performance.now())) : undefined
    if (changed) this.notify()
  }

  private onEnded = () => {
    if (this.mode !== 'calm' || MUSIC_SCENES[this.scene].bed === 'battle' || this.silent()) return
    this.calmTime = 0
    this.cursor = (this.cursor + 1) % this.calm.length
    void this.play(this.calmUrl(), false, 0)
  }

  private onError = () => {
    if (this.mode !== 'calm' || this.audio.error?.code === MediaError.MEDIA_ERR_ABORTED) return
    this.misses += 1
    if (this.misses > this.calm.length) {
      this.audio.pause()
      return
    }
    this.cursor = (this.cursor + 1) % this.calm.length
    this.calmTime = 0
    void this.play(this.calmUrl(), false, 0)
  }

  private onVisibility = () => {
    if (!this.unlocked) return
    if (document.hidden) this.audio.pause()
    else if (!this.silent()) void this.follow()
  }

  private async follow() {
    const battle = MUSIC_SCENES[this.scene].bed === 'battle'
    const playingRaid = this.mode === 'battle' && this.audio.src.includes(BATTLE)
    const playingCalm = this.mode === 'calm' && this.audio.src.includes('/assets/music/part_')
    if ((battle && playingRaid) || (!battle && playingCalm)) {
      this.applyVolume()
      this.resumeContext()
      if (this.audio.paused) await this.audio.play().catch(() => undefined)
      return
    }
    if (this.mode === 'calm' && this.audio.currentSrc.includes('/assets/music/part_')) this.calmTime = this.audio.currentTime
    this.mode = battle ? 'battle' : 'calm'
    await this.play(battle ? this.battle : this.calmUrl(), battle, battle ? 0 : this.calmTime)
  }

  private async play(url: string, loop: boolean, at: number) {
    const token = ++this.token
    const absolute = new URL(url, window.location.href).href
    this.audio.loop = loop
    this.applyVolume()
    if (this.audio.src !== absolute) this.audio.src = url
    if (at > 0.25) {
      const seek = () => {
        if (token !== this.token || !Number.isFinite(this.audio.duration)) return
        this.audio.currentTime = Math.min(at, Math.max(0, this.audio.duration - 0.5))
      }
      if (this.audio.readyState >= 1) seek()
      else this.audio.addEventListener('loadedmetadata', seek, { once: true })
    }
    if (token !== this.token || this.silent()) return
    this.resumeContext()
    await this.audio.play().catch(() => undefined)
  }
}

let singleton: BackgroundMusic | null = null

export function backgroundMusic() {
  singleton ??= new BackgroundMusic()
  return singleton
}
