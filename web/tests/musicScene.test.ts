import test from 'node:test'
import assert from 'node:assert/strict'
import { MUSIC_SCENES, MusicSceneState } from '../src/game/musicScene.ts'
import { cityRaidPlaying, dungeonMusicScene, villageMusicScene } from '../src/game/musicContext.ts'
import type { Citizen, DungeonRunView, DungeonSnapshot, VillageState } from '../src/api.ts'

test('all 21 score scenes and both farm variations have an explicit legacy fallback', () => {
  assert.equal(Object.keys(MUSIC_SCENES).filter(s => /^M\d\d$/.test(s)).length, 21)
  assert.equal(MUSIC_SCENES.farmNight.bed, 'calm')
  assert.equal(MUSIC_SCENES.M15.bed, 'battle')
})

test('combat interrupts immediately; a transient end and repeated snapshot do not chatter the scene', () => {
  const state = new MusicSceneState()
  assert.equal(state.request('M02', 0), 'M02')
  assert.equal(state.request('M11', 10), 'M11')
  assert.equal(state.request('M09', 20), 'M11')
  assert.equal(state.request('M09', 1_000), 'M11')
  assert.equal(state.nextChangeAt(), 2_020)
  assert.equal(state.request('M11', 1_500), 'M11')
  assert.equal(state.nextChangeAt(), null)
  assert.equal(state.request('M09', 1_600), 'M11')
  assert.equal(state.tick(3_599), 'M11')
  assert.equal(state.tick(3_600), 'M09')
})

test('equal ambient priorities hold eight seconds; escalation to raid and siege never waits', () => {
  const state = new MusicSceneState()
  state.request('M02', 100)
  assert.equal(state.request('M06', 1_000), 'M02')
  assert.equal(state.nextChangeAt(), 8_100)
  assert.equal(state.tick(8_100), 'M06')
  assert.equal(state.request('M13', 8_101), 'M13')
  assert.equal(state.request('M15', 8_102), 'M15')
})

test('warning, terminal and unknown raid phases do not announce a new battle', () => {
  for (const phase of ['Warning', 'Finished', 'Cancelled', 'unexpected'])
    assert.equal(cityRaidPlaying([{ phase, finishedAt: null }]), false)
  for (const phase of ['Approaching', 'Fighting', 'Raiding', 'Retreating'])
    assert.equal(cityRaidPlaying([{ phase, finishedAt: null }]), true)
  assert.equal(cityRaidPlaying([{ phase: 'Fighting', finishedAt: '2026-10-21T00:00:00Z' }]), false)
})

const village = { weather: { night: false }, battles: [], defeatedAt: null } as unknown as VillageState
const until = new Date(100_000).toISOString()

test('world priority follows live raid, defeat, actual rest/recovery, journey, useful farm work and day/night', () => {
  const farmer: Partial<Citizen> = { taskId: 'sow-turnips', state: 'Working', cycleEndsAt: until }
  assert.equal(villageMusicScene(village, farmer, 1), 'farmDay')
  assert.equal(villageMusicScene({ ...village, weather: { ...village.weather, night: true } }, farmer, 1), 'farmNight')
  assert.equal(villageMusicScene(village, { ...farmer, state: 'AwaitingRenewal' }, 1), 'M02')
  assert.equal(villageMusicScene(village, farmer, 100_001), 'M02')
  assert.equal(villageMusicScene(village, { ...farmer, restState: 'RestRequested' }, 1), 'farmDay')
  assert.equal(villageMusicScene(village, { ...farmer, restState: 'Resting' }, 1), 'M20')
  assert.equal(villageMusicScene(village, { injuredUntil: until }, 1), 'M20')
  const journey = { arrivesAt: until, blockedAt: null } as NonNullable<Citizen['journey']>
  assert.equal(villageMusicScene(village, { ...farmer, journey }, 1), 'M07')
  assert.equal(villageMusicScene(village, { journey: { ...journey, blockedAt: until } }, 1), 'M02')
  assert.equal(villageMusicScene({ ...village, defeatedAt: until }, { restState: 'Resting' }, 1), 'M17')
  assert.equal(villageMusicScene({ ...village, defeatedAt: until, battles: [{ phase: 'Fighting', finishedAt: null }] as VillageState['battles'] }, null, 1), 'M13')
})

const view = { runId: 'current-run', kind: 'Group', run: { phase: 'Running', outcome: 'Pending' } } as DungeonRunView
const snapshot = { runId: view.runId, outcome: 'Pending', heroes: [{ id: 'hero', health: 10, state: 'Fighting', targetFoe: null }],
  foes: [{ id: 1, health: 10, target: null }] } as unknown as DungeonSnapshot

test('dungeon combat requires current live server targets, including a foe attacking a withdrawing hero', () => {
  assert.equal(dungeonMusicScene(view, null), 'M09')
  assert.equal(dungeonMusicScene(view, snapshot), 'M09')
  const fighting = { ...snapshot, foes: [{ ...snapshot.foes[0], target: 'hero' }] }
  assert.equal(dungeonMusicScene(view, fighting), 'M11')
  assert.equal(dungeonMusicScene(view, { ...fighting, runId: 'stale-run' }), 'M09')
  assert.equal(dungeonMusicScene(view, { ...fighting, outcome: 'Victory' }), 'M09')
  assert.equal(dungeonMusicScene(view, { ...fighting, heroes: [{ ...snapshot.heroes[0], health: 0 }] }), 'M09')
  assert.equal(dungeonMusicScene(view, { ...fighting, heroes: [{ ...snapshot.heroes[0], state: 'Left' }] }), 'M09')
  assert.equal(dungeonMusicScene(view, { ...fighting, heroes: [{ ...snapshot.heroes[0], retreating: true }] }), 'M11')
  assert.equal(dungeonMusicScene(view, { ...snapshot, heroes: [{ ...snapshot.heroes[0], targetFoe: 1 }] }), 'M11')
  assert.equal(dungeonMusicScene({ ...view, run: { ...view.run, phase: 'Ended', outcome: 'Victory' } }, fighting), 'M19')
  assert.equal(dungeonMusicScene({ ...view, run: { ...view.run, phase: 'Marching' } }, fighting), 'M12')
})
