import { createRoot } from 'react-dom/client'
import { useEffect, useRef, useState } from 'react'
import { DungeonWorld } from '../src/game/DungeonWorld'
import { BrowserRouter } from 'react-router'
import { DungeonHud, DungeonCameraControls } from '../src/pages/DungeonPage'
import { I18nProvider } from '../src/i18n'
import type { DungeonRunView, DungeonSnapshot } from '../src/api'
import type { DungeonCameraStand } from './dungeon-camera-contract'
import '../src/index.css'

function Stand() {
  const host = useRef<HTMLDivElement>(null)
  const [world, setWorld] = useState<DungeonWorld | null>(null)
  const [hud, setHud] = useState<{view: DungeonRunView; snapshot: DungeonSnapshot} | null>(null)
  useEffect(() => {
    let disposed = false, created: DungeonWorld | null = null
    DungeonWorld.create(host.current!).then(instance => {
      if (disposed) { instance.destroy(); return }
      created = instance
      const internals = instance as unknown as {
        app: { ticker: { stop(): void } }; scale: number; following: boolean
        world: { x: number; y: number; toGlobal(p: { x: number; y: number }): { x: number; y: number } }
        update(ticker: { deltaMS: number }): void
      }
      const ambush = new URLSearchParams(location.search).has('ambush')
      const view = { runId: 'camera-test', slug: 'camera-test', kind: ambush ? 'Ambush' : 'Group', encounterEnemy: ambush ? 'Wolf' : null, timeLimitSeconds: 60,
        width: ambush ? 12 : 20, height: ambush ? 9 : 20, tiles: '0'.repeat(ambush ? 108 : 400), entrance: ambush ? {x: .5, y: 4.5} : { x: 10, y: 10 },
        myHeroId: 'hero', heroes: [{ id: 'hero', citizenId: 'hero', name: 'Traveller', class: 'Brawler', maximumHealth: 40, appearance: null }],
        run: { phase: 'Running', foes: 1, slain: 0, heroes: [{ citizenId: 'hero', arrivesAt: null }] } } as DungeonRunView
      instance.setRun(view)
      const show = (x: number, y: number) => {
        const snapshot = { ...view, elapsed: 0, floor: 0, floorCount: 1, outcome: 'Pending', events: [],
          heroes: [{ id: 'hero', x, y, health: 40, state: 'Fighting', targetFoe: null, healTarget: null, kills: 0, experience: 0, loot: [], arrows: null }],
          foes: [{ id: 1, kind: 'Wolf', x: ambush ? 8.5 : 11, y: ambush ? 4.5 : 9, health: 18, maximumHealth: 18, target: null }],
        } as unknown as DungeonSnapshot
        instance.setSnapshot(snapshot)
        setHud({view, snapshot})
        internals.update({ deltaMS: 1000 })
      }
      window.dungeonCameraOrders = []
      instance.setHandlers({ onFoe: id => window.dungeonCameraOrders.push('attack:' + id),
        onHero: id => window.dungeonCameraOrders.push('heal:' + id), onGround: () => window.dungeonCameraOrders.push('move') })
      show(ambush ? 1.5 : 10, ambush ? 4.5 : 10)
      window.dungeonCameraStand = {
        read: () => ({ scale: internals.scale, tilePixels: internals.scale * 16, following: internals.following, x: internals.world.x, y: internals.world.y }),
        point: (x, y) => { const p = internals.world.toGlobal({ x: x * 16, y: y * 16 }); return { x: p.x, y: p.y } },
        hero: show,
      } satisfies DungeonCameraStand
      setWorld(instance)
    })
    return () => { disposed = true; created?.destroy() }
  }, [])
  return <div className="game-root dungeon-root"><div ref={host} className="game-canvas" /><div className="hud">{hud && <DungeonHud view={hud.view} snapshot={hud.snapshot} backTo="/v/test" onRetreat={() => window.dungeonCameraOrders.push("retreat")} />}<DungeonCameraControls world={world} hasHero /></div></div>
}
createRoot(document.getElementById('app')!).render(<I18nProvider><BrowserRouter><Stand /></BrowserRouter></I18nProvider>)
