import type { Citizen, DungeonRunView, DungeonSnapshot, Tsar, VillageState } from '../api'
import type { MusicScene } from './musicScene.ts'

/** Warning is not a battle. Unknown or terminal states cannot manufacture a combat cue. */
export function cityRaidPlaying(battles: readonly { finishedAt: string | null; phase: string }[] | undefined) {
  return !!battles?.some(b => !b.finishedAt && ['Approaching', 'Fighting', 'Raiding', 'Retreating'].includes(b.phase))
}

type MusicActor = Partial<Pick<Citizen, 'state' | 'taskId' | 'cycleEndsAt' | 'recovery'> &
  Pick<Tsar, 'restState' | 'injuredUntil' | 'journey'>>

export function villageMusicScene(village: Pick<VillageState, 'battles' | 'weather' | 'defeatedAt'>,
  actor: MusicActor | null | undefined, now: number): MusicScene {
  if (cityRaidPlaying(village.battles)) return 'M13'
  if (village.defeatedAt) return 'M17'
  if (actor?.restState === 'Resting' || actor?.injuredUntil && Date.parse(actor.injuredUntil) > now ||
    actor?.recovery?.until && Date.parse(actor.recovery.until) > now) return 'M20'
  if (actor?.journey && !actor.journey.blockedAt && Date.parse(actor.journey.arrivesAt) > now) return 'M07'
  const farm = actor?.state === 'Working' && !!actor.cycleEndsAt && Date.parse(actor.cycleEndsAt) > now &&
    (actor.taskId?.startsWith('sow-') || ['tend-fields', 'water-fields', 'harvest'].includes(actor.taskId ?? ''))
  if (farm) return village.weather.night ? 'farmNight' : 'farmDay'
  return village.weather.night ? 'M06' : 'M02'
}

export function dungeonMusicScene(view: Pick<DungeonRunView, 'run' | 'kind' | 'runId'> | null | undefined,
  snapshot: DungeonSnapshot | null): MusicScene {
  if (!view) return 'M09'
  if (view.run.phase === 'Ended') return view.run.outcome === 'Victory' ? 'M19' : 'M09'
  if (view.run.phase === 'Marching') return 'M12'
  if (view.run.phase !== 'Running') return 'M09'
  // Only the current server run, live bodies and a real target establish combat. Selecting an enemy does not.
  if (snapshot?.runId === view.runId && snapshot.outcome === 'Pending') {
    const heroes = new Set(snapshot.heroes.filter(h => h.health > 0 && h.state !== 'Left').map(h => h.id))
    const foes = snapshot.foes.filter(f => f.health > 0)
    if (foes.some(f => f.target !== null && heroes.has(f.target)) ||
      snapshot.heroes.some(h => heroes.has(h.id) && h.targetFoe !== null && foes.some(f => f.id === h.targetFoe))) return 'M11'
  }
  return 'M09'
}
