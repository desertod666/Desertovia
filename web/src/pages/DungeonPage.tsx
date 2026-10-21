import { useReportContext } from '../reports/context'
import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router'
import { api, type DungeonRunView, type DungeonSnapshot, type Me } from '../api'
import { DungeonWorld } from '../game/DungeonWorld'
import { useDungeonRun } from '../hooks'
import { useI18n } from '../i18n'
import { LoginPanel } from '../ui/LoginPanel'
import {Hint} from '../ui/Hint'

const ZOOM_STEP = 1.25

function clock(seconds: number) {
  const whole = Math.max(0, Math.ceil(seconds))
  return `${Math.floor(whole / 60)}:${String(whole % 60).padStart(2, '0')}`
}

/** Anyone signed in may watch a run; the heroes' players fight in it. */
export function DungeonPage() {
  const { t } = useI18n()
  const [me, setMe] = useState<Me | null | undefined>(undefined)
  const loadMe = useCallback(async () => setMe(await api.me()), [])
  useEffect(() => {
    api.me().then(setMe)
  }, [])

  if (me === undefined) return <div className="splash">{t('loading')}</div>
  if (!me) {
    return (
      <div className="menu-screen">
        <LoginPanel onLoggedIn={loadMe} />
        <Link to="/">{t('toHome')}</Link>
      </div>
    )
  }
  return <DungeonView />
}

function DungeonView() {
  const { slug = '', runId = '' } = useParams()
  const i18n = useI18n()
  const { t } = i18n
  const { view, snapshot, error, moveTo, attack, heal,retreat } = useDungeonRun(runId)
  const hostRef = useRef<HTMLDivElement>(null)
  const [world, setWorld] = useState<DungeonWorld | null>(null)
  useReportContext(() => ({ slug, page: 'dungeon', dungeonId: runId }))

  useEffect(() => {
    const host = hostRef.current
    if (!host) return
    let disposed = false
    let created: DungeonWorld | null = null
    DungeonWorld.create(host).then((instance) => {
      if (disposed) {
        instance.destroy()
        return
      }
      created = instance
      setWorld(instance)
    })
    return () => {
      disposed = true
      created?.destroy()
      setWorld(null)
    }
  }, [])

  useEffect(() => {
    if (world && view) world.setRun(view)
  }, [world, view])
  useEffect(() => {
    if (world && snapshot) world.setSnapshot(snapshot)
  }, [world, snapshot])

  const myHeroId = view?.myHeroId ?? null
  const myClass = view?.heroes.find((hero) => hero.id === myHeroId)?.class ?? null
  useEffect(() => {
    if (!world) return
    world.setHandlers(
      myHeroId
        ? {
            onFoe: attack,
            onHero: (heroId) => (myClass === 'Healer' ? heal(heroId) : undefined),
            onGround: moveTo,
          }
        : null,
    )
  }, [world, myHeroId, myClass, attack, heal, moveTo])

  const backTo = `/v/${encodeURIComponent(view?.slug ?? slug)}`
  return (
    <div className="game-root dungeon-root">
      <div ref={hostRef} className="game-canvas" />
      <div className="hud">
        {view === null || error ? (
          <div className="dungeon-top">
            <span>{error ? i18n.error(error) : t('dungeon.notFound')}</span>
            <Link className="button" to={backTo}>
              {t('dungeon.back')}
            </Link>
          </div>
        ) : view ? (
          <DungeonHud view={view} snapshot={snapshot} backTo={backTo} onRetreat={retreat}/>
        ) : null}
        <DungeonCameraControls world={world} hasHero={!!myHeroId} />
      </div>
      {view && view.run.phase === 'Ended' && <DungeonResult view={view} backTo={backTo} />}
    </div>
  )
}

export function DungeonCameraControls({ world, hasHero }: { world: Pick<DungeonWorld, 'zoomBy' | 'focusHero'> | null; hasHero: boolean }) {
  const { t } = useI18n()
  return <div className="zoom-controls">
    <button onClick={() => world?.zoomBy(ZOOM_STEP)} aria-label={t('hud.zoomIn')}>+</button>
    <button onClick={() => world?.zoomBy(1 / ZOOM_STEP)} aria-label={t('hud.zoomOut')}>−</button>
    {hasHero && <button onClick={() => world?.focusHero()} aria-label={t('hud.locate')}>◎</button>}
  </div>
}

export function DungeonHud({ view, snapshot, backTo,onRetreat }: { view: DungeonRunView; snapshot: DungeonSnapshot | null; backTo: string;onRetreat:()=>void }) {
  const i18n = useI18n()
  const { t } = i18n
  const hero = view.heroes.find((candidate) => candidate.id === view.myHeroId) ?? null
  const live = snapshot?.heroes.find((candidate) => candidate.id === view.myHeroId) ?? null
  const foesLeft = snapshot ? snapshot.foes.filter((foe) => foe.health > 0).length : view.run.foes - view.run.slain
  const target = live?.targetFoe != null ? snapshot?.foes.find((foe) => foe.id === live.targetFoe) : undefined
  const running = view.run.phase === 'Running' && snapshot !== null
  const floorName = view.kind==='Ambush'?(i18n.language==='ru'?'Засада':'Ambush')+(view.encounterEnemy?` · ${i18n.enemy(view.encounterEnemy)}`:''):!snapshot
    ? t('dungeon.deadLands')
    : snapshot.floor === 2
      ? t('dungeon.floor.barrow')
      : snapshot.floor === 1
        ? t('dungeon.floor.graves')
        : snapshot.floorCount > 1
          ? t('dungeon.floor.woods')
          : t('dungeon.deadLands')
  let hint: string
  if (!running) hint = view.run.phase === 'Ended' ? '' : t('dungeon.waiting')
  else if (!hero) hint = t('dungeon.hintWatcher')
  else if (live?.state === 'Fleeing') hint = t('dungeon.fled')
  else if (live?.state === 'Left') hint = view.kind==='Ambush'?(i18n.language==='ru'?'Встреча закончилась. Можно вернуться в мир.':'The encounter has ended. Return to the world.'):t('dungeon.out')
  else if(live?.retreating)hint=i18n.language==='ru'?'Герой отходит к выходу.':'Your hero is withdrawing to the exit.'
  else hint = hero.class === 'Healer' ? t('dungeon.hintHealer') : t('dungeon.hintFighter')

  return (
    <>
      <div className="dungeon-top">
        <strong>{floorName}</strong>
        {running && snapshot && snapshot.floorCount > 1 && (
          <span>{t('dungeon.floor', { floor: snapshot.floor + 1, count: snapshot.floorCount })}</span>
        )}
        {running && <span>{t('dungeon.timeLeft', { time: clock(view.timeLimitSeconds - snapshot.elapsed) })}</span>}
        <span>{t('dungeon.foesLeft', { count: foesLeft })}</span>
        <Link className="button" to={backTo}>
          {t('dungeon.back')}
        </Link>
      </div>
      {hint && <div className="dungeon-hint small">{hint}</div>}
      {hero && live && (
        <div className="card dungeon-hero">
          {running&&live.state==='Fighting'&&<div className="row dungeon-retreat"><button disabled={!!live.retreating} onClick={onRetreat}>{i18n.language==='ru'?(live.retreating?'Отступаю…':'Отступить'):(live.retreating?'Withdrawing…':'Retreat')}</button><Hint label={i18n.language==='ru'?'Об отступлении':'About retreat'} text={i18n.language==='ru'?'Герой идёт к входу и остаётся уязвимым до выхода. Закрытие страницы не завершает бой.':'Your hero walks to the entrance and remains vulnerable until leaving. Closing this page does not end the fight.'}>ⓘ</Hint></div>}
          <div>
            <strong>{hero.name}</strong> · {i18n.heroClass(hero.class)}
          </div>
          <div className="bar thin">
            <div style={{ width: `${(live.health / Math.max(1, hero.maximumHealth)) * 100}%` }} />
          </div>
          <div className="small">
            {Math.ceil(live.health)} / {Math.round(hero.maximumHealth)}
            {target && ` · ⚔ ${i18n.enemy(target.kind)}`}
          </div>
          <div className="small">
            {t('dungeon.kills', { count: live.kills })} · {t('dungeon.experience', { amount: live.experience })}
            {live.arrows !== null && ` · ${t('dungeon.arrows', { count: live.arrows })}`}
          </div>
          <div className="small muted">
            {live.loot.length > 0 ? t('dungeon.loot', { goods: i18n.goods(live.loot) }) : t('dungeon.noLoot')}
          </div>
        </div>
      )}
    </>
  )
}

export function DungeonResult({ view, backTo }: { view: DungeonRunView; backTo: string }) {
  const i18n = useI18n()
  const { t } = i18n
  const run = view.run
  if(view.kind==='Ambush')return <div className="modal-backdrop"><div className="modal dungeon-result" role="dialog" aria-label={i18n.language==='ru'?'Итог засады':'Encounter result'}>
    <header className="modal-header"><h2>{i18n.language==='ru'?(run.outcome==='Victory'?'Противник повержен':'Встреча завершена'):(run.outcome==='Victory'?'Enemy defeated':'Encounter ended')}</h2></header>
    <div className="modal-body"><p>{view.workSettlementPending?(i18n.language==='ru'?'Результат задания сохранён и ожидает завершения восстановления.':'The work result is saved and waiting for recovery to finish.'):(i18n.language==='ru'?'Исход учтён в результате задания.':'The outcome has been included in your work result.')}</p>
    <Link className="button primary" to={backTo}>{i18n.language==='ru'?'Вернуться в мир':'Return to the world'}</Link></div>
  </div></div>
  return (
    <div className="modal-backdrop">
      <div className="modal dungeon-result" role="dialog" aria-label={t(`dungeon.result.${run.outcome}`)}>
        <header className="modal-header">
          <h2>{t(`dungeon.result.${run.outcome}`)}</h2>
        </header>
        <div className="modal-body">
          <p>{t('dungeon.result.slain', { slain: run.slain, foes: run.foes })}</p>
          <ul className="feed">
            {run.heroes.map((hero) => (
              <li key={hero.citizenId ?? 'tsar'}>
                {t('dungeon.result.hero', { name: hero.name, heroClass: i18n.heroClass(hero.class), kills: hero.kills })}
                {hero.loot.length > 0 && ` · ${t('dungeon.loot', { goods: i18n.goods(hero.loot) })}`}
                {hero.experience > 0 && ` · ${t('dungeon.experience', { amount: hero.experience })}`}
                {hero.killer && ` · ${t('dungeon.result.wounded', { enemy: i18n.enemy(hero.killer).toLowerCase() })}`}
                {hero.broken.length > 0 &&
                  ` · ${t('dungeon.result.broken', { goods: hero.broken.map((kind) => i18n.resource(kind)).join(', ') })}`}
              </li>
            ))}
          </ul>
          <Link className="button primary" to={backTo}>
            {t('dungeon.back')}
          </Link>
        </div>
      </div>
    </div>
  )
}
