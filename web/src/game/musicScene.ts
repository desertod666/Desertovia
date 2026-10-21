/** Scene IDs follow MUSIC_PROMPTS.txt. Files for the new score are not available yet. */
export const MUSIC_SCENES = {
  M01: { priority: 0, bed: 'calm' }, // main theme
  M02: { priority: 10, bed: 'calm' }, // village day
  M03: { priority: 10, bed: 'calm' }, // market and workshops
  M04: { priority: 10, bed: 'calm' }, // tavern
  M05: { priority: 20, bed: 'calm' }, // royal return
  M06: { priority: 10, bed: 'calm' }, // village night
  M07: { priority: 30, bed: 'calm' }, // forest and road
  M08: { priority: 30, bed: 'calm' }, // fog
  M09: { priority: 40, bed: 'calm' }, // dungeon exploration
  M10: { priority: 40, bed: 'calm' }, // undead settlement
  M11: { priority: 80, bed: 'battle' }, // personal encounter
  M12: { priority: 30, bed: 'calm' }, // expedition
  M13: { priority: 90, bed: 'battle' }, // village raid
  M14: { priority: 95, bed: 'battle' }, // boss
  M15: { priority: 100, bed: 'battle' }, // last siege
  M16: { priority: 70, bed: 'calm' }, // divine intervention
  M17: { priority: 60, bed: 'calm' }, // ruins
  M18: { priority: 50, bed: 'calm' }, // rebuilding
  M19: { priority: 45, bed: 'calm' }, // return
  M20: { priority: 20, bed: 'calm' }, // healing and safe rest
  M21: { priority: 10, bed: 'calm' }, // arcade
  farmDay: { priority: 25, bed: 'calm' },
  farmNight: { priority: 25, bed: 'calm' },
} as const

export type MusicScene = keyof typeof MUSIC_SCENES
export type MusicBed = typeof MUSIC_SCENES[MusicScene]['bed']

/** One stable scene. Higher urgency wins immediately; transient exits cannot chatter the player. */
export class MusicSceneState {
  private current: MusicScene | null = null
  private enteredAt = 0
  private desired: MusicScene | null = null
  private requestedAt = 0

  request(scene: MusicScene, now: number) {
    if (this.desired !== scene) { this.desired = scene; this.requestedAt = now }
    return this.tick(now)
  }

  tick(now: number) {
    if (this.desired === null) return this.current
    if (this.current === null || this.current !== this.desired &&
      (MUSIC_SCENES[this.desired].priority > MUSIC_SCENES[this.current].priority || now >= this.nextChangeAt()!)) {
      this.current = this.desired
      this.enteredAt = now
    }
    return this.current
  }

  nextChangeAt(): number | null {
    if (this.current === null || this.desired === null || this.current === this.desired) return null
    if (MUSIC_SCENES[this.desired].priority > MUSIC_SCENES[this.current].priority) return this.requestedAt
    const combat = MUSIC_SCENES[this.current].bed === 'battle'
    return Math.max(this.enteredAt + (combat ? 0 : 8_000), this.requestedAt + (combat ? 2_000 : 500))
  }
}
